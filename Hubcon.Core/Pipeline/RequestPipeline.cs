using Hubcon.Core.Converters;
using Hubcon.Core.MethodHandling;
using Hubcon.Core.Models;
using Hubcon.Core.Models.Interfaces;
using System.ComponentModel;
using System.Text.Json;

namespace Hubcon.Core.Handlers
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class RequestPipeline : IRequestPipeline
    {
        private readonly MethodInvokerProvider _methodInvokerProvider;
        private readonly DynamicConverter _converter;
        private readonly IMiddlewareProvider _middlewareProvider;

        public RequestPipeline(
            MethodInvokerProvider methodInvokerProvider, 
            DynamicConverter dynamicConverter, 
            IMiddlewareProvider middlewareProvider)
        {
            _methodInvokerProvider = methodInvokerProvider;
            _converter = dynamicConverter;
            _middlewareProvider = middlewareProvider;
        }

        public Task HandleWithoutResultAsync(object instance, MethodInvokeRequest request)
        {            
            Func<Task<IMethodResponse?>> method = async () =>
            {
                _methodInvokerProvider.GetMethodInvoker(request.MethodName, instance.GetType(), out var methodInvoker);
                object?[] args = _converter.DeserializeJsonArgs(request.Args, methodInvoker!.ParameterTypes).ToArray();
                object? result = methodInvoker?.Method?.DynamicInvoke(instance, args);

                if (result is Task task)
                    await task;

                return new BaseMethodResponse(true);
            };

            var pipeline = _middlewareProvider.GetPipeline(instance.GetType(), request, method);

            return pipeline.Execute();
        }

        public Task<IMethodResponse> HandleSynchronousResult(object instance, MethodInvokeRequest request)
        {
            Func<Task<IMethodResponse>> method = async () =>
            {
                return await Task.Run(() =>
                {
                    _methodInvokerProvider.GetMethodInvoker(request.MethodName, instance.GetType(), out var methodInvoker);
                    object?[] args = _converter.DeserializeJsonArgs(request.Args, methodInvoker!.ParameterTypes).ToArray();
                    object? result = methodInvoker?.Method?.DynamicInvoke(instance, args);

                    if (result is null)
                        return new BaseMethodResponse(true);

                    return new BaseMethodResponse(true, _converter.SerializeObject(result));
                });
            };

            var pipeline = _middlewareProvider.GetPipeline(instance.GetType(), request, method!);
            return pipeline.Execute();
        }

        public Task HandleSynchronous(object instance, MethodInvokeRequest request)
        {
            Func<Task<IMethodResponse?>> method = () =>
            {
                return Task.Run<IMethodResponse?>(async () =>
                {
                    _methodInvokerProvider.GetMethodInvoker(request.MethodName, instance.GetType(), out var methodInvoker);
                    object?[] args = _converter.DeserializeJsonArgs(request.Args, methodInvoker!.ParameterTypes).ToArray();
                    object? result = methodInvoker?.Method?.DynamicInvoke(instance, args);

                    if (result is Task task)
                        await task;

                    return new BaseMethodResponse(true);
                });
            };

            var pipeline = _middlewareProvider.GetPipeline(instance.GetType(), request, method);
            return pipeline.Execute();
        }

        public IAsyncEnumerable<JsonElement?> GetStream(object instance, MethodInvokeRequest request)
        {
            Func<Task<IMethodResponse?>> method = async () =>
            {
                _methodInvokerProvider.GetMethodInvoker(request.MethodName, instance.GetType(), out var methodInvoker);
                object?[] args = _converter.DeserializeJsonArgs(request.Args, methodInvoker!.ParameterTypes).ToArray();
                object? result = methodInvoker?.Method?.DynamicInvoke(instance, args);
                var response = new BaseMethodResponse(true, result);
                return response;
            };
            
            var pipeline = _middlewareProvider.GetPipeline(instance.GetType(), request, method);
            var pipelineResult = (IAsyncEnumerable<object>)pipeline.Execute().Result.Data!;
            return _converter.ConvertToJsonElementStream(pipelineResult);
        }

        public Task<IMethodResponse> HandleWithResultAsync(object instance, MethodInvokeRequest request)
        {
            Func<Task<IMethodResponse>> method = async () =>
            {
                _methodInvokerProvider.GetMethodInvoker(request.MethodName, instance.GetType(), out var methodInvoker);
                object?[] args = _converter.DeserializeJsonArgs(request.Args, methodInvoker!.ParameterTypes).ToArray();
                object? result = methodInvoker?.Method?.DynamicInvoke(instance, args);

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

            var pipeline = _middlewareProvider.GetPipeline(instance.GetType(), request, method!);
            return pipeline.Execute();
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

        public void RegisterMethods(Type type, Action<HubconMethodInvoker>? forEachMethodAction = null) => _methodInvokerProvider.RegisterMethods(type, forEachMethodAction);
    }
}
