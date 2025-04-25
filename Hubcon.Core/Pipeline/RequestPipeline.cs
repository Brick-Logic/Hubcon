using Hubcon.Core.Converters;
using Hubcon.Core.MethodHandling;
using Hubcon.Core.Models;
using Hubcon.Core.Models.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Text.Json;

namespace Hubcon.Core.Handlers
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class ControllerInvocationHandler : IControllerInvocationHandler
    {
        private readonly MethodInvokerProvider _methodInvokerProvider;
        private readonly DynamicConverter _converter;
        private readonly IServiceProvider _serviceProvider;
        private readonly IMiddlewareProvider _middlewareProvider;

        public ControllerInvocationHandler(
            MethodInvokerProvider methodInvokerProvider,
            DynamicConverter dynamicConverter,
            IServiceProvider serviceProvider,
            IMiddlewareProvider middlewareProvider)
        {
            _methodInvokerProvider = methodInvokerProvider;
            _converter = dynamicConverter;
            _serviceProvider = serviceProvider;
            _middlewareProvider = middlewareProvider;
        }

        public async Task<IResponse> HandleWithoutResultAsync(MethodInvokeRequest request)
        {
            if (!_methodInvokerProvider.GetMethodInvoker(request, out var methodInvoker))
                return new BaseMethodResponse(false);

            var controller = _serviceProvider.GetRequiredService(methodInvoker!.ControllerType);

            Func<Task<IMethodResponse?>> method = async () =>
            {
                object?[] args = _converter.DeserializeJsonArgs(request.Args, methodInvoker!.ParameterTypes).ToArray();
                object? result = methodInvoker?.Method?.DynamicInvoke(controller, args);

                if (result is Task task)
                    await task;

                return new BaseMethodResponse(true);
            };

            var pipeline = _middlewareProvider.GetPipeline(methodInvoker, request, method);

            return (IResponse)await pipeline.Execute();
        }

        public async Task<BaseJsonResponse> HandleSynchronousResult(MethodInvokeRequest request)
        {
            if (!_methodInvokerProvider.GetMethodInvoker(request, out var methodInvoker))
                return new BaseJsonResponse(false);

            var controller = _serviceProvider.GetRequiredService(methodInvoker!.ControllerType);

            Func<Task<IMethodResponse>> method = async () =>
            {
                return await Task.Run(() =>
                {
                    object?[] args = _converter.DeserializeJsonArgs(request.Args, methodInvoker!.ParameterTypes).ToArray();
                    object? result = methodInvoker?.Method?.DynamicInvoke(controller, args);

                    if (result is null)
                        return new BaseMethodResponse(true);

                    return new BaseMethodResponse(true, _converter.SerializeObject(result));
                });
            };

            var pipeline = _middlewareProvider.GetPipeline(methodInvoker, request, method!);
            var result = await pipeline.Execute();
            return new BaseJsonResponse(result.Success, _converter.SerializeObject(result.Data));
        }

        public async Task<IResponse> HandleSynchronous(MethodInvokeRequest request)
        {
            if (!_methodInvokerProvider.GetMethodInvoker(request, out var methodInvoker))
                return new BaseMethodResponse(false);

            var controller = _serviceProvider.GetRequiredService(methodInvoker!.ControllerType);

            Func<Task<IMethodResponse?>> method = () =>
            {
                return Task.Run<IMethodResponse?>(async () =>
                {
                    object?[] args = _converter.DeserializeJsonArgs(request.Args, methodInvoker!.ParameterTypes).ToArray();
                    object? result = methodInvoker?.Method?.DynamicInvoke(controller, args);

                    if (result is Task task)
                        await task;

                    return new BaseMethodResponse(true);
                });
            };

            var pipeline = _middlewareProvider.GetPipeline(methodInvoker, request, method);
            return await pipeline.Execute();
        }

        public IAsyncEnumerable<JsonElement?> GetStream(MethodInvokeRequest request)
        {
            if (!_methodInvokerProvider.GetMethodInvoker(request, out var methodInvoker))
                return null!;

            var controller = _serviceProvider.GetRequiredService(methodInvoker!.ControllerType);

            Func<Task<IMethodResponse?>> method = async () =>
            {
                object?[] args = _converter.DeserializeJsonArgs(request.Args, methodInvoker!.ParameterTypes).ToArray();
                object? result = methodInvoker?.Method?.DynamicInvoke(controller, args);
                var response = new BaseMethodResponse(true, result);
                return response;
            };

            var pipeline = _middlewareProvider.GetPipeline(methodInvoker, request, method);
            var pipelineResult = (IAsyncEnumerable<object>)pipeline.Execute().Result.Data!;
            return _converter.ConvertToJsonElementStream(pipelineResult);
        }

        public async Task<BaseJsonResponse> HandleWithResultAsync(MethodInvokeRequest request)
        {
            if (!_methodInvokerProvider.GetMethodInvoker(request, out var methodInvoker))
                return null!;

            var controller = _serviceProvider.GetRequiredService(methodInvoker!.ControllerType);

            Func<Task<IMethodResponse>> method = async () =>
            {
                object?[] args = _converter.DeserializeJsonArgs(request.Args, methodInvoker!.ParameterTypes).ToArray();
                object? result = methodInvoker?.Method?.DynamicInvoke(controller, args);

                if (result is null)
                    return new BaseMethodResponse(true);
                else if (result is Task task)
                {
                    var response = await GetTaskResultAsync(task, methodInvoker!.ReturnType.GetGenericArguments()[0]);
                    return new BaseMethodResponse(true, _converter.SerializeObject(response));
                }
                else
                    return new BaseMethodResponse(true, _converter.SerializeObject(result));
            };

            var pipeline = _middlewareProvider.GetPipeline(methodInvoker, request, method!);
            var result = await pipeline.Execute();
            return new BaseJsonResponse(result.Success, _converter.SerializeObject(result.Data));
        }

        public static async Task<object?> GetTaskResultAsync(Task taskObject, Type returnType)
        {
            // Esperar a que el Task termine
            await taskObject;

            // Verificar si es un Task<T> (Task con resultado)
            var taskType = taskObject.GetType();

            if (taskType.IsGenericType)
            {
                // Obtener el tipo del resultado (T)
                var resultProperty = taskType.GetProperty("Result");

                // Obtener el resultado del Task
                var result = resultProperty?.GetValue(taskObject);

                return Convert.ChangeType(result, returnType);
            }

            // Si no es un Task<T>, no hay valor que devolver
            return null;
        }
    }
}
