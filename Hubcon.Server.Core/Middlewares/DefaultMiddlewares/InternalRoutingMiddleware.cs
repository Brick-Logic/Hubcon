using Hubcon.Server.Abstractions.CustomAttributes;
using Hubcon.Server.Abstractions.Delegates;
using Hubcon.Server.Abstractions.Enums;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Core.Configuration;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Core.Tools;
using Hubcon.Shared.Core.Websockets.Events;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Text.Json;
using System.Threading.Channels;
using KeyNotFoundException = System.Collections.Generic.KeyNotFoundException;

namespace Hubcon.Server.Core.Middlewares.DefaultMiddlewares
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class InternalRoutingMiddleware(
        IServiceProvider serviceProvider,
        IDynamicConverter dynamicConverter,
        IInternalServerOptions options,
        ILogger<InternalRoutingMiddleware> logger,
        ILiveSubscriptionRegistry liveSubscriptionRegistry) : IInternalRoutingMiddleware
    {
        public async Task Execute(IOperationRequest request, IOperationContext context, ResultHandlerDelegate resultHandler, PipelineDelegate next)
        {
            if (context.Blueprint.Kind == OperationKind.Method 
                || context.Blueprint.Kind == OperationKind.Stream 
                || context.Blueprint.Kind == OperationKind.Ingest)
            {
                if(context.Request.Arguments?.Count != context.Blueprint!.ParameterTypes.Count)
                {
                    context.Result = new BaseOperationResponse<object>(false);
                    return;
                }

                foreach(var kvp in context.Request.Arguments)
                {
                    var type = context.Blueprint!.ParameterTypes[kvp.Key];

                    if (context.Request.Arguments[kvp.Key] is JsonElement element)
                    {
                        context.Request.Arguments[kvp.Key] = dynamicConverter.DeserializeJsonElement(element, type);
                    }
                    else if (EnumerableTools.IsAsyncEnumerable(context.Request.Arguments[kvp.Key]!)
                        && EnumerableTools.GetAsyncEnumerableType(context.Request.Arguments[kvp.Key]!) == typeof(IAsyncEnumerable<JsonElement>))
                    {
                        context.Request.Arguments[kvp.Key] = EnumerableTools.ConvertAsyncEnumerableDynamic(
                            type,
                            (IAsyncEnumerable<JsonElement>)context.Request.Arguments[kvp.Key]!,
                            dynamicConverter);

                        continue;
                    }
                    else if (context.Request.Arguments[kvp.Key]?.GetType().IsAssignableTo(type) ?? false)
                    {
                        continue;
                    }
                    else
                    {
                        context.Result = new BaseOperationResponse<object>(false);
                        return;
                    }
                }

                var controller = serviceProvider.GetRequiredService(context.Blueprint!.ControllerType);
                object? result = context.Blueprint!.InvokeDelegate?.Invoke(controller, context.Request.Arguments.Values.ToArray()!);
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
                    string websocketToken = context.HttpContext?.Request.Headers.Authorization.ToString()!;

                    if (options.WebsocketRequiresAuthorization && context.HttpContext?.User == null)
                        throw new UnauthorizedAccessException();

                    clientId = websocketToken;

                    subDescriptor = liveSubscriptionRegistry.GetHandler(websocketToken, request.ContractName, request.OperationName);


                    if (subDescriptor == null)
                    {
                        var subscription = (ISubscription)context.RequestServices.GetRequiredService(context.Blueprint.RawReturnType);

                        if (subscription is null)
                            throw new InvalidOperationException($"No se encontró un servicio que implemente la interfaz {nameof(ISubscription)}.");


                        subDescriptor = liveSubscriptionRegistry.RegisterHandler(websocketToken, request.ContractName, request.OperationName, subscription);
                    }
                }

                context.Blueprint.ConfigurationAttributes.TryGetValue(typeof(SubscriptionSettingsAttribute), out Attribute? attribute);
                var subSettings = (attribute as SubscriptionSettingsAttribute)?.Settings ?? SubscriptionSettings.Default;

                var channelOptions = new BoundedChannelOptions(subSettings.ChannelCapacity)
                {
                    Capacity = subSettings.ChannelCapacity,
                    FullMode = subSettings.ChannelFullMode,
                    SingleReader = false,
                    SingleWriter = false,
                    AllowSynchronousContinuations = true
                };

                var observer = new AsyncObserver<object>(channelOptions);

                async Task hubconEventHandler(object? eventValue)
                {
                    try
                    {
                        await observer.WriteToChannelAsync(eventValue!);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex.Message);
                    }
                }

                subDescriptor.Subscription.AddGenericHandler(hubconEventHandler);

                async IAsyncEnumerable<object?> SubDelegate()
                {
                    try
                    {
                        await foreach (var newEvent in observer.GetAsyncEnumerable(default))
                        {
                            yield return newEvent;

                            if (subSettings.ThrottleDelay > TimeSpan.Zero)
                                await Task.Delay(subSettings.ThrottleDelay);
                        }
                    }
                    finally
                    {
                        observer.OnCompleted();
                        liveSubscriptionRegistry.RemoveHandler(clientId, request.ContractName, request.OperationName);
                        subDescriptor.Subscription.RemoveGenericHandler(hubconEventHandler);
                    };
                };

                context.Result = new BaseOperationResponse<object>(true, SubDelegate());
                await next();
            }
        }
    }
}
