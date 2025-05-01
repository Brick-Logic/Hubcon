using Hubcon.Core.Converters;
using Hubcon.Core.MethodHandling;
using Hubcon.Core.Models;
using Hubcon.Core.Models.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.IdentityModel.Tokens.Jwt;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text.Json;

namespace Hubcon.Core.Handlers
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

        public async Task<IResponse> HandleWithoutResultAsync(MethodInvokeRequest request)
        {
            if (!_methodDescriptorProvider.GetMethodDescriptor(request, out MethodDescriptor? descriptor))
                return new BaseMethodResponse(false);

            var controller = _serviceProvider.GetRequiredService(descriptor!.ControllerType);

            async Task<IMethodResponse?> MethodInvocation()
            {
                object?[] args = _converter.DeserializeJsonArgs(request.Args, descriptor!.ParameterTypes).ToArray();
                object? result = descriptor.Method?.DynamicInvoke(controller, args);

                if (result is Task task)
                    await task;

                return new BaseMethodResponse(true);
            }

            var pipeline = _middlewareProvider.GetPipeline(descriptor, request, MethodInvocation);

            return (IResponse)await pipeline.Execute();
        }

        public async Task<BaseJsonResponse> HandleSynchronousResult(MethodInvokeRequest request)
        {
            if (!_methodDescriptorProvider.GetMethodDescriptor(request, out MethodDescriptor? descriptor))
                return new BaseJsonResponse(false);

            var controller = _serviceProvider.GetRequiredService(descriptor!.ControllerType);

            Task<IMethodResponse> MethodInvocation()
            {
                object?[] args = _converter.DeserializeJsonArgs(request.Args, descriptor!.ParameterTypes).ToArray();
                object? result = descriptor.Method?.DynamicInvoke(controller, args);

                if (result is null)
                    return Task.FromResult((IMethodResponse)new BaseMethodResponse(true));

                return Task.FromResult((IMethodResponse)new BaseMethodResponse(true, _converter.SerializeObject(result)));
            }

            var pipeline = _middlewareProvider.GetPipeline(descriptor, request, MethodInvocation!);
            var result = await pipeline.Execute();
            return new BaseJsonResponse(result.Success, _converter.SerializeObject(result.Data));
        }

        public async Task<IResponse> HandleSynchronous(MethodInvokeRequest request)
        {
            if (!_methodDescriptorProvider.GetMethodDescriptor(request, out MethodDescriptor? descriptor))
                return new BaseMethodResponse(false);

            var controller = _serviceProvider.GetRequiredService(descriptor!.ControllerType);

            async Task<IMethodResponse?> MethodInvocation()
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

        public IAsyncEnumerable<JsonElement?> GetStream(MethodInvokeRequest request)
        {
            if (!_methodDescriptorProvider.GetMethodDescriptor(request, out MethodDescriptor? descriptor))
                return null!;

            var controller = _serviceProvider.GetRequiredService(descriptor!.ControllerType);

            Task<IMethodResponse?> MethodInvocation()
            {
                object?[] args = _converter.DeserializeJsonArgs(request.Args, descriptor!.ParameterTypes).ToArray();
                object? result = descriptor.Method?.DynamicInvoke(controller, args);
                var response = new BaseMethodResponse(true, result);
                return Task.FromResult((IMethodResponse?)response);
            }

            var pipeline = _middlewareProvider.GetPipeline(descriptor, request, MethodInvocation);
            var pipelineResult = (IAsyncEnumerable<object>)pipeline.Execute().Result.Data!;
            return _converter.ConvertToJsonElementStream(pipelineResult);
        }

        public async IAsyncEnumerable<JsonElement?> GetSubscription(
            SubscriptionRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var accessor = _serviceProvider.GetRequiredService<IHttpContextAccessor>();
            var jwtToken = ExtractTokenFromHeader(accessor.HttpContext!);

            var jwtHandler = new JwtSecurityTokenHandler();

            //if (!jwtHandler.CanReadToken(jwtToken))
            //    throw new UnauthorizedAccessException();

            //JwtSecurityToken? token = jwtHandler.ReadJwtToken(jwtToken);
            //var userIdClaim = token.Claims.FirstOrDefault(c =>c.Type == ClaimTypes.NameIdentifier || c.Type == "sub");

            //if (userIdClaim?.Value is not string userId)
            //    throw new UnauthorizedAccessException("No se encontró el ID de usuario en el token.");

            var userId = "test";

            var handler = _subscriptionRegistry.GetHandler(userId, request.ContractName, request.SubscriptionName);

            if (handler == null)
            {
                handler = _serviceProvider.GetRequiredService<ISubscription>();

                if (handler is null)
                    throw new InvalidOperationException($"No se encontró un servicio que implemente la interfaz {nameof(ISubscription)}.");
 
                _subscriptionRegistry.RegisterHandler(userId, request.ContractName, request.SubscriptionName, handler);
            }

            var observer = new AsyncObserver<object>();
            
            HubconEventHandler hubconEventHandler = async (eventValue) =>
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

            handler += hubconEventHandler;

            try
            {
                await foreach (var newEvent in observer.GetAsyncEnumerable(cancellationToken))
                {
                    yield return _converter.SerializeObject(newEvent);
                }
            }
            finally
            {
                _subscriptionRegistry.RemoveHandler(userId, request.ContractName, request.SubscriptionName);
                handler -= hubconEventHandler;
            }
        }


        public async Task<BaseJsonResponse> HandleWithResultAsync(MethodInvokeRequest request)
        {
            if (!_methodDescriptorProvider.GetMethodDescriptor(request, out MethodDescriptor? descriptor))
                return null!;

            var controller = _serviceProvider.GetRequiredService(descriptor!.ControllerType);

            Func<Task<IMethodResponse>> method = async () =>
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

        string? ExtractTokenFromHeader(HttpContext httpContext)
        {
            var authHeader = httpContext.Request.Headers["Authorization"].ToString();

            if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return authHeader.Substring("Bearer ".Length).Trim();
            }

            return null;
        }
    }
}
