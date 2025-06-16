using Hubcon.Server.Abstractions.Delegates;
using Hubcon.Server.Abstractions.Enums;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Core.Subscriptions;
using Hubcon.Shared.Core.Tools;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using KeyNotFoundException = System.Collections.Generic.KeyNotFoundException;

namespace Hubcon.Server.Core.Middlewares.DefaultMiddlewares
{
    public class InternalRoutingMiddleware(
        IServiceProvider serviceProvider,
        IDynamicConverter dynamicConverter,
        ILiveSubscriptionRegistry liveSubscriptionRegistry) : IInternalRoutingMiddleware
    {

        public async Task Execute(IOperationRequest request, IOperationContext context, ResultHandlerDelegate resultHandler, PipelineDelegate next)
        {
            if (context.Blueprint.Kind == OperationKind.Method || context.Blueprint.Kind == OperationKind.Stream)
            {
                if(context.Arguments.Length != context.Blueprint!.ParameterTypes.Length)
                {
                    context.Result = new BaseOperationResponse(false);
                    return;
                }

                for(int i = 0; i < context.Arguments.Length; i++)
                {
                    var type = context.Blueprint!.ParameterTypes[i];

                    if (context.Arguments[i] is JsonElement)
                    {
                        context.Arguments[i] = dynamicConverter.DeserializeJsonElement((JsonElement)context.Arguments[i]!, type);
                    }
                    else if (EnumerableTools.IsAsyncEnumerable(context.Arguments[i]!) 
                        && EnumerableTools.GetAsyncEnumerableType(context.Arguments[i]!) == typeof(IAsyncEnumerable<JsonElement>))
                    {
                        context.Arguments[i] = await EnumerableTools.ConvertAsyncEnumerableDynamic(
                            type, 
                            (IAsyncEnumerable<JsonElement>)context.Arguments[i]!, 
                            dynamicConverter);

                        continue;
                    }
                    else if (context.Arguments[i]?.GetType().IsAssignableTo(type) ?? false)
                    {
                        continue;
                    }
                    else
                    {
                        context.Result = new BaseOperationResponse(false);
                        return;
                    }
                }

                var controller = serviceProvider.GetRequiredService(context.Blueprint!.ControllerType);
                object? result = context.Blueprint!.InvokeDelegate?.Invoke(controller, context.Arguments!);

                context.Result = await resultHandler.Invoke(result);
                await next();
            }
            else if (context.Blueprint.Kind == OperationKind.Subscription)
            {
                string clientId = "";

                if (context.Blueprint.OperationInfo == null) throw new KeyNotFoundException($"Suscripcion no encontrada.");

                ISubscriptionDescriptor? subDescriptor = null;

                if (!context.Blueprint.RequiresAuthorization)
                {
                    subDescriptor = liveSubscriptionRegistry.GetHandler("", request.ContractName, request.OperationName);

                    if (subDescriptor == null)
                    {
                        var subscription = (ISubscription?)context.RequestServices.GetRequiredService(context.Blueprint.RawReturnType);

                        subDescriptor = liveSubscriptionRegistry.RegisterHandler("", request.ContractName, request.OperationName, subscription);
                    }
                }
                else
                {
                    string? jwtToken = JwtHelper.ExtractTokenFromHeader(context.HttpContext);
                    string? userId = JwtHelper.GetUserId(jwtToken);

                    if (userId == null)
                        throw new UnauthorizedAccessException();

                    clientId = userId;

                    subDescriptor = liveSubscriptionRegistry.GetHandler(userId, request.ContractName, request.OperationName);


                    if (subDescriptor == null)
                    {
                        var subscription = (ISubscription)context.RequestServices.GetRequiredService(context.Blueprint.RawReturnType);

                        if (subscription is null)
                            throw new InvalidOperationException($"No se encontró un servicio que implemente la interfaz {nameof(ISubscription)}.");


                        subDescriptor = liveSubscriptionRegistry.RegisterHandler(userId, request.ContractName, request.OperationName, subscription);
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

                subDescriptor.Subscription.AddGenericHandler(hubconEventHandler);

                async IAsyncEnumerable<object?> SubDelegate()
                {
                    try
                    {
                        await foreach (var newEvent in observer.GetAsyncEnumerable(new()))
                        {
                            yield return newEvent;
                        }
                    }
                    finally
                    {
                        liveSubscriptionRegistry.RemoveHandler(clientId, request.ContractName, request.OperationName);
                        subDescriptor.Subscription.RemoveGenericHandler(hubconEventHandler);
                    };
                };

                context.Result = new BaseOperationResponse(true, SubDelegate());
                await next();
            }
        }
    }
}
