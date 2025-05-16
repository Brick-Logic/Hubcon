using Hubcon.Core.Abstractions.Delegates;
using Hubcon.Core.Abstractions.Enums;
using Hubcon.Core.Abstractions.Interfaces;
using Hubcon.Core.Exceptions;
using Hubcon.Core.Invocation;
using Hubcon.Core.Subscriptions;
using Hubcon.Core.Tools;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace Hubcon.Core.Middlewares.DefaultMiddlewares
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
                var controller = serviceProvider.GetRequiredService(context.Blueprint!.ControllerType);

                object?[] args = dynamicConverter.DeserializeJsonArgs(request.Args, context.Blueprint!.ParameterTypes).ToArray();
                object? result = context.Blueprint!.InvokeDelegate?.DynamicInvoke(controller, args);

                context.Result = await resultHandler.Invoke(result);
                await next();
            }
            else if (context.Blueprint.Kind == OperationKind.Subscription)
            {
                string clientId = "";

                if (context.Blueprint.OperationInfo == null) throw new HubconRemoteException($"Suscripcion no encontrada.");

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

                async IAsyncEnumerable<JsonElement?> SubDelegate()
                {
                    try
                    {
                        await foreach (var newEvent in observer.GetAsyncEnumerable(new()))
                        {
                            yield return dynamicConverter.SerializeObject(newEvent);
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
