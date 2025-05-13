using GreenDonut;
using Hubcon.Core.Abstractions.Delegates;
using Hubcon.Core.Abstractions.Enums;
using Hubcon.Core.Abstractions.Interfaces;
using Hubcon.Core.Attributes;
using Hubcon.Core.Exceptions;
using Hubcon.Core.Invocation;
using Hubcon.Core.Subscriptions;
using Hubcon.Core.Tools;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

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
                    subDescriptor = liveSubscriptionRegistry.GetHandler(clientId, request.ContractName, request.OperationName);

                    if (subDescriptor == null)
                    {
                        var subscription = context.RequestServices.GetRequiredService<ISubscription>();

                        subDescriptor = liveSubscriptionRegistry.RegisterHandler(clientId, request.ContractName, request.OperationName, subscription);
                    }
                }
                else
                {
                    var accessor = context.RequestServices.GetService<IHttpContextAccessor>();
                    string? jwtToken = JwtHelper.ExtractTokenFromHeader(accessor?.HttpContext!);
                    string? userId = JwtHelper.GetUserId(jwtToken);

                    if (userId == null)
                        throw new UnauthorizedAccessException();

                    clientId = userId;

                    subDescriptor = liveSubscriptionRegistry.GetHandler(userId, request.ContractName, request.OperationName);


                    if (subDescriptor == null)
                    {
                        var subscription = context.RequestServices.GetRequiredService<ISubscription>();

                        if (subscription is null)
                            throw new InvalidOperationException($"No se encontró un servicio que implemente la interfaz {nameof(ISubscription)}.");


                        subDescriptor = liveSubscriptionRegistry.RegisterHandler(userId, request.ContractName, request.OperationName, subscription);
                    }
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

                async IAsyncEnumerable<JsonElement?> SubDelegate()
                {
                    try
                    {
                        subDescriptor.Subscription.AddHandler(hubconEventHandler);
                        await foreach (var newEvent in observer.GetAsyncEnumerable(context.RequestAborted))
                        {
                            yield return dynamicConverter.SerializeObject(newEvent);
                        }
                    }
                    finally
                    {
                        liveSubscriptionRegistry.RemoveHandler(clientId, request.ContractName, request.OperationName);
                        subDescriptor.Subscription.RemoveHandler(hubconEventHandler);
                    };
                };

                context.Result = new BaseOperationResponse(true, SubDelegate());
                await next();
            }
        }
    }
}
