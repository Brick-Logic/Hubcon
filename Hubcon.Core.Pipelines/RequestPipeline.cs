using Hubcon.Core.Abstractions;
using Hubcon.Core.Abstractions.Delegates;
using Hubcon.Core.Abstractions.Interfaces;
using Hubcon.Core.Attributes;
using Hubcon.Core.Exceptions;
using Hubcon.Core.Extensions;
using Hubcon.Core.Invocation;
using Hubcon.Core.Subscriptions;
using Hubcon.Core.Tools;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Hubcon.Core.Pipelines
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class ControllerInvocationHandler : IControllerInvocationHandler
    {
        private readonly IMethodDescriptorProvider _methodDescriptorProvider;
        private readonly IDynamicConverter _converter;
        private readonly IServiceProvider _serviceProvider;
        private readonly IMiddlewareProvider _middlewareProvider;
        private readonly ISubscriptionRegistry _subscriptionRegistry;

        public ControllerInvocationHandler(
            IMethodDescriptorProvider methodDescriptorProvider,
            IDynamicConverter dynamicConverter,
            IServiceProvider serviceProvider,
            IMiddlewareProvider middlewareProvider,
            ISubscriptionRegistry subscriptionRegistry)
        {
            _methodDescriptorProvider = methodDescriptorProvider;
            _converter = dynamicConverter;
            _serviceProvider = serviceProvider;
            _middlewareProvider = middlewareProvider;
            _subscriptionRegistry = subscriptionRegistry;
        }

        public async Task<IResponse> HandleWithoutResultAsync(IMethodInvokeRequest request)
        {
            if (!_methodDescriptorProvider.GetMethodDescriptor(request, out IMethodDescriptor? descriptor))
                return new BaseMethodResponse(false);

            var controller = _serviceProvider.GetRequiredService(descriptor!.ControllerType);

            async Task<IObjectMethodResponse?> MethodInvocation()
            {
                object?[] args = _converter.DeserializeJsonArgs(request.Args, descriptor!.ParameterTypes).ToArray();
                object? result = descriptor.Method?.DynamicInvoke(controller, args);

                if (result is Task task)
                    await task;

                return new BaseMethodResponse(true);
            }

            var pipeline = _middlewareProvider.GetPipeline(descriptor, request, MethodInvocation);

            return await pipeline.Execute();
        }

        public async Task<IMethodResponse<JsonElement>> HandleSynchronousResult(IMethodInvokeRequest request)
        {
            if (!_methodDescriptorProvider.GetMethodDescriptor(request, out IMethodDescriptor? descriptor))
                return new BaseJsonResponse(false);

            var controller = _serviceProvider.GetRequiredService(descriptor!.ControllerType);

            Task<IObjectMethodResponse> MethodInvocation()
            {
                object?[] args = _converter.DeserializeJsonArgs(request.Args, descriptor!.ParameterTypes).ToArray();
                object? result = descriptor.Method?.DynamicInvoke(controller, args);

                if (result is null)
                    return Task.FromResult((IObjectMethodResponse)new BaseMethodResponse(true));

                return Task.FromResult((IObjectMethodResponse)new BaseMethodResponse(true, _converter.SerializeObject(result)));
            }

            var pipeline = _middlewareProvider.GetPipeline(descriptor, request, MethodInvocation!);
            var result = await pipeline.Execute();
            return new BaseJsonResponse(result.Success, _converter.SerializeObject(result.Data));
        }

        public async Task<IResponse> HandleSynchronous(IMethodInvokeRequest request)
        {
            if (!_methodDescriptorProvider.GetMethodDescriptor(request, out IMethodDescriptor? descriptor))
                return new BaseMethodResponse(false);

            var controller = _serviceProvider.GetRequiredService(descriptor!.ControllerType);

            async Task<IObjectMethodResponse?> MethodInvocation()
            {
                object?[] args = _converter.DeserializeJsonArgs(request.Args, descriptor!.ParameterTypes).ToArray();
                object? result = descriptor.Method?.DynamicInvoke(controller, args);

                if (result is Task task)
                    await task;

                return new BaseMethodResponse(true);
            }

            var pipeline = _middlewareProvider.GetPipeline(descriptor, request, MethodInvocation);
            return await pipeline.Execute();
        }

        public IAsyncEnumerable<JsonElement?> GetStream(IMethodInvokeRequest request)
        {
            if (!_methodDescriptorProvider.GetMethodDescriptor(request, out IMethodDescriptor? descriptor))
                return null!;

            var controller = _serviceProvider.GetRequiredService(descriptor!.ControllerType);

            Task<IObjectMethodResponse?> MethodInvocation()
            {
                object?[] args = _converter.DeserializeJsonArgs(request.Args, descriptor!.ParameterTypes).ToArray();
                object? result = descriptor.Method?.DynamicInvoke(controller, args);
                var response = new BaseMethodResponse(true, result);
                return Task.FromResult((IObjectMethodResponse?)response);
            }

            var pipeline = _middlewareProvider.GetPipeline(descriptor, request, MethodInvocation);
            var pipelineResult = (IAsyncEnumerable<object>)pipeline.Execute().Result.Data!;
            return _converter.ConvertToJsonElementStream(pipelineResult);
        }

        public async IAsyncEnumerable<JsonElement?> GetSubscription(
            ISubscriptionRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            string clientId = "";

            var info = _subscriptionRegistry.GetSubscriptionMetadata(request.ContractName, request.SubscriptionName);

            if (info == null) throw new HubconRemoteException($"Suscripcion no encontrada.");

            ISubscriptionDescriptor? subDescriptor = null;

            if (info.HasCustomAttribute<AllowAnonymousAttribute>())
            {
                clientId = "";

                subDescriptor = _subscriptionRegistry.GetHandler(clientId, request.ContractName, request.SubscriptionName);

                if (subDescriptor == null)
                {
                    var subscription = (ISubscription)_serviceProvider.GetRequiredService(info.PropertyType);

                    subDescriptor = _subscriptionRegistry.RegisterHandler(clientId, request.ContractName, request.SubscriptionName, subscription);
                }
            }
            else
            {
                var accessor = _serviceProvider.GetService<IHttpContextAccessor>();
                string? jwtToken = JwtHelper.ExtractTokenFromHeader(accessor?.HttpContext!);
                string? userId = JwtHelper.GetUserId(jwtToken);

                if (userId == null)
                    throw new UnauthorizedAccessException();

                clientId = userId ;

                subDescriptor = _subscriptionRegistry.GetHandler(userId, request.ContractName, request.SubscriptionName);


                if (subDescriptor == null)
                {
                    var subscription = (ISubscription)_serviceProvider.GetRequiredService(info.PropertyType);

                    if (subscription is null)
                        throw new InvalidOperationException($"No se encontró ningun servicio de suscripción.");


                    subDescriptor = _subscriptionRegistry.RegisterHandler(userId, request.ContractName, request.SubscriptionName, subscription);
                }
            }

            var observer = new AsyncObserver<object>();

            HubconEventHandler<object> hubconEventHandler = async (eventValue) =>
            {
                try
                {
                    await observer.WriteToChannelAsync(eventValue!);               
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error al escribir al canal de observación: {ex}");
                }
            };

            try
            {
                subDescriptor.Subscription.AddGenericHandler(hubconEventHandler);
                await foreach (var newEvent in observer.GetAsyncEnumerable(cancellationToken))
                {
                    yield return _converter.SerializeObject(newEvent);
                }
            }
            finally
            {
                subDescriptor.Subscription.RemoveGenericHandler(hubconEventHandler);
            }
        }


        public async Task<IMethodResponse<JsonElement>> HandleWithResultAsync(IMethodInvokeRequest request)
        {
            if (!_methodDescriptorProvider.GetMethodDescriptor(request, out IMethodDescriptor? descriptor))
                return null!;

            var controller = _serviceProvider.GetRequiredService(descriptor!.ControllerType);

            InvocationDelegate method = async () =>
            {
                object?[] args = _converter.DeserializeJsonArgs(request.Args, descriptor!.ParameterTypes).ToArray();
                object? result = descriptor.Method?.DynamicInvoke(controller, args);

                if (result is null)
                    return new BaseMethodResponse(true);
                else if (result is Task task)
                {
                    var response = await GetTaskResultAsync(task, descriptor!.ReturnType.GetGenericArguments()[0]);
                    return new BaseMethodResponse(true, _converter.SerializeObject(response));
                }
                else
                    return new BaseMethodResponse(true, _converter.SerializeObject(result));
            };

            var pipeline = _middlewareProvider.GetPipeline(descriptor, request, method!);
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
