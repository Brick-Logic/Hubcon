using Hubcon.Server.Abstractions.Delegates;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Server.Core.Extensions;
using Hubcon.Shared.Core.Tools;
using Hubcon.Shared.Core.Websockets.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Threading.Channels;

namespace Hubcon.Server.Core.Middlewares.DefaultMiddlewares
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class InternalRoutingMiddleware(
        IServiceProvider serviceProvider,
        IDynamicConverter dynamicConverter,
        IInternalServerOptions options,
        IOperationRegistry operationRegistry,
        ILogger<InternalRoutingMiddleware> logger,
        ILiveSubscriptionRegistry liveSubscriptionRegistry) : IInternalRoutingMiddleware
    {
        public async Task Execute(IOperationRequest request, IOperationContext context, ResultHandlerDelegate resultHandler, PipelineDelegate next)
        {
            if (context.Blueprint.Kind == OperationKind.CallMethod
                || context.Blueprint.Kind == OperationKind.InvokeMethod
                || context.Blueprint.Kind == OperationKind.Stream
                || context.Blueprint.Kind == OperationKind.Ingest)
            {
                var dict = context.Request.Arguments.ToDictionary();

                if (context.Blueprint.Kind == OperationKind.Ingest)
                {
                    foreach (var kvp in context.Blueprint!.ParameterTypes)
                    {
                        if (!context.Blueprint!.ParameterTypes.TryGetValue(kvp.Key, out var type))
                        {
                            continue;
                        }
                        if (dict.TryGetValue(kvp.Key, out var item) && item is JsonElement element)
                        {
                            dict[kvp.Key] = dynamicConverter.DeserializeJsonElement(element, type)!;
                        }
                        else if (EnumerableTools.IsAsyncEnumerable(dict[kvp.Key]!)
                            && EnumerableTools.GetAsyncEnumerableType(dict[kvp.Key]!) == typeof(IAsyncEnumerable<JsonElement>))
                        {
                            dict[kvp.Key] = EnumerableTools.ConvertAsyncEnumerableDynamic(
                                type,
                                ((IAsyncEnumerable<JsonElement>)dict[kvp.Key]!),
                                dynamicConverter);

                            continue;
                        }
                    }
                }
                else
                {
                    foreach (var kvp in context.Blueprint!.ParameterTypes)
                    {
                        if (context.Blueprint!.ParameterTypes.TryGetValue(kvp.Key, out var type)
                            && dict.TryGetValue(kvp.Key, out var item)
                            && item is JsonElement element)
                        {
                            dict[kvp.Key] = dynamicConverter.DeserializeJsonElement(element, type)!;
                        }
                    }
                }

                var controller = context.Blueprint!.ControllerFactory.Invoke(serviceProvider, null);

                if (context.Blueprint.HasSubscriptions)
                {
                    var subRegistry = context.RequestServices.GetRequiredService<ILiveSubscriptionRegistry>();

                    string identity = "anonymous";
                    if (context.Blueprint.RequiresAuthorization)
                    {
                        if (context.HttpContext!.Request.Headers.TryGetValue("Authorization", out var authHeader))
                        {
                            string rawValue = authHeader.ToString();
                            identity = rawValue;
                        }
                    }

                    foreach (var sub in context.Blueprint.SubscriptionProperties)
                    {
                        if (!operationRegistry.GetOperationBlueprint(context.Blueprint.SimpleContractName, sub.PropInfo.Name, HubconTransportAttribute.GetDefault<WebSocketTransport>(), out IOperationBlueprint? blueprint))
                            continue;

                        object? subInstance = null;
                        var descriptor = subRegistry.GetHandler(identity, context.Blueprint.SimpleContractName, blueprint!.OperationName);
                        subInstance = descriptor?.Subscription;
                        sub.FastSetter.Invoke(controller, subInstance);
                    }
                }

                var wrapper = Activator.CreateInstance(context.Blueprint!.CallWrapperType!)!;
                context.Blueprint!.WrapperMapper!.Invoke(dict, wrapper, context.RequestAborted);

                var validationResults = new List<ValidationResult>();
                var validationContext = new ValidationContext(wrapper);
                if (!Validator.TryValidateObject(wrapper, validationContext, validationResults, true))
                {
                    // Si hay errores, devolvemos un 400 Bad Request con los detalles
                    var errors = validationResults.ToDictionary(
                        k => k.MemberNames.FirstOrDefault() ?? "error",
                        v => new[] { v.ErrorMessage ?? "Invalid value" }
                    );

                    context.Response = HubconResponse.BadRequest(errors, null, "Validation errors detected.");
                    return;
                }

                static bool RecordDiagnostics(Exception ex, IOperationContext context)
                {
                    context.Exception = ex;
                    return false;
                }

                object? result = null;

                try
                {
                    result = context.Blueprint!.InvokeDelegate?.Invoke(controller, wrapper);
                }
                catch (Exception ex) when (RecordDiagnostics(ex, context))
                {
                }

                var response = await resultHandler.Invoke(result);
                context.Response = (response as IHubconResponse)!;
                await next();
            }
            else if (context.Blueprint.Kind == OperationKind.Subscription)
            {
                string clientId = "";

                if (context.Blueprint.MemberInfo == null)
                {
                    context.Response = HubconResponse.NotFound(error: "Suscripcion no encontrada");
                    return;
                }

                ISubscriptionDescriptor? subDescriptor = null;

                if (!context.Blueprint.RequiresAuthorization)
                {
                    subDescriptor = liveSubscriptionRegistry.GetHandler("", context.Blueprint.SimpleContractName, context.Blueprint.OperationName);

                    if (subDescriptor == null)
                    {
                        var subscription = (ISubscription?)context.RequestServices.GetRequiredService(context.Blueprint.RawReturnType);
                        subDescriptor = liveSubscriptionRegistry.RegisterHandler("", context.Blueprint.SimpleContractName, context.Blueprint.OperationName, subscription!);
                    }
                }
                else
                {
                    string websocketToken = context.HttpContext?.Request.Headers.Authorization.ToString()!;

                    if (options.WebsocketRequiresAuthorization && context.HttpContext?.User == null)
                    {
                        context.Response = HubconResponse.Unauthorized();
                        return;
                    }

                    clientId = websocketToken;

                    subDescriptor = liveSubscriptionRegistry.GetHandler(websocketToken, context.Blueprint.SimpleContractName, context.Blueprint.OperationName);


                    if (subDescriptor == null)
                    {
                        var subscription = (ISubscription)context.RequestServices.GetRequiredService(context.Blueprint.RawReturnType);

                        if (subscription is null)
                        {
                            context.Response = HubconResponse.InternalError();
                            return;
                        }

                        subDescriptor = liveSubscriptionRegistry.RegisterHandler(websocketToken, context.Blueprint.SimpleContractName, context.Blueprint.OperationName, subscription);
                    }
                }

                context.Blueprint.ConfigurationAttributes.TryGetValue(typeof(RateLimitAttribute), out Attribute? attribute);
                var subSettings = (attribute as RateLimitAttribute) ?? new RateLimitAttribute();

                var channelOptions = new BoundedChannelOptions(subSettings.QueueLimit)
                {
                    Capacity = subSettings.QueueLimit,
                    FullMode = BoundedChannelFullMode.Wait,
                    SingleReader = false,
                    SingleWriter = false,
                    AllowSynchronousContinuations = true
                };

                IAsyncObserver<object>? observer = AsyncObserver.Create<object>(dynamicConverter, channelOptions);

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

                await subDescriptor.Subscription.AddGenericHandler(hubconEventHandler);

                async IAsyncEnumerable<object?> SubDelegate()
                {
                    try
                    {
                        await foreach (var newEvent in observer.GetAsyncEnumerable(default))
                        {
                            yield return newEvent;
                        }
                    }
                    finally
                    {
                        observer.OnCompleted();
                        liveSubscriptionRegistry.RemoveHandler(clientId, context.Blueprint.SimpleContractName, context.Blueprint.OperationName);
                        await subDescriptor.Subscription.RemoveGenericHandler(hubconEventHandler);
                    }
                };

                context.Response = HubconResponse.Ok(SubDelegate());
                await next();
            }
        }
    }
}
