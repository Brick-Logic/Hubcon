using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Core.Entrypoint;
using Hubcon.Server.Core.Pipelines;
using Hubcon.Server.Core.Pipelines.UpgradedPipeline;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Core.Websockets;
using Hubcon.Shared.Core.Websockets.Events;
using Hubcon.Shared.Core.Websockets.Heartbeat;
using Hubcon.Shared.Core.Websockets.Interfaces;
using Hubcon.Shared.Core.Websockets.Messages.Connection;
using Hubcon.Shared.Core.Websockets.Messages.Generic;
using Hubcon.Shared.Core.Websockets.Messages.Ingest;
using Hubcon.Shared.Core.Websockets.Messages.Operation;
using Hubcon.Shared.Core.Websockets.Messages.Ping;
using Hubcon.Shared.Core.Websockets.Messages.Streams;
using Hubcon.Shared.Core.Websockets.Messages.Token;
using Hubcon.Shared.Core.Websockets.Models;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Channels;
using Hubcon.Server.Core.WebSockets.Middleware;

namespace Hubcon.Server.Core.Websockets.Middleware
{
    /// <summary>
    /// The hubcon websocket middleware, used to handle hubcon websocket connections.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class HubconWebSocketMiddleware
    {
        private int clientCount;
        private readonly RequestDelegate next;
        private readonly IInternalServerOptions options;
        private readonly IConnectionSupervisor connectionSupervisor;

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="next"></param>
        /// <param name="connectionSupervisor"></param>
        /// <param name="options"></param>
        /// <param name="telemetryProvider"></param>
        public HubconWebSocketMiddleware(
            RequestDelegate next,
            IConnectionSupervisor connectionSupervisor,
            IInternalServerOptions options,
            ITelemetryProvider telemetryProvider)
        {
            this.connectionSupervisor = connectionSupervisor;
            this.next = next;
            this.options = options;

            telemetryProvider.RegisterProvider(x => x.GetCurrentWebsocketClients, () => clientCount);
        }

        /// <summary>
        /// Begins the execution of the pipeline.
        /// </summary>
        /// <param name="httpContext"></param>
        /// <param name="serviceProvider"></param>
        /// <returns></returns>
        public async Task InvokeAsync(HttpContext httpContext, IServiceProvider serviceProvider)
        {
            if (!httpContext.WebSockets.IsWebSocketRequest ||
                !(httpContext.Request.Path == options.WebSocketPathPrefix))
            {
                await next(httpContext);
                return;
            }

            var corsService = httpContext.RequestServices.GetRequiredService<ICorsService>();
            var corsPolicyProvider = httpContext.RequestServices.GetRequiredService<ICorsPolicyProvider>();

            var policy = await corsPolicyProvider.GetPolicyAsync(httpContext, null);

            if (policy != null)
            {
                var corsResult = corsService.EvaluatePolicy(httpContext, policy);

                if (!corsResult.IsOriginAllowed)
                {
                    httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return;
                }

                corsService.ApplyResult(corsResult, httpContext.Response);
            }

            long lastTokenExpirationDate;
            string connectionId = Guid.NewGuid().ToString();
            WebSocket webSocket = null!;

            var userData = await IsAuthorized(httpContext, options);

            if (userData != null)
            {
                if (userData.Value.AccessToken != null)
                    httpContext.Request.Headers.Authorization = userData.Value.AccessToken;

                httpContext.User = userData.Value.ClaimsPrincipal;
                lastTokenExpirationDate = userData.Value.ExpirationTime;
                connectionSupervisor.Register(connectionId, userData.Value.ExpirationTime, webSocket.Abort);
            }
            else
            {
                return;
            }

            try
            {
                webSocket = await httpContext.WebSockets.AcceptWebSocketAsync();
            }
            catch (Exception)
            {
                return;
            }

            ClientWebSocketContext context = new ClientWebSocketContext(httpContext);
            context.Initialize(connectionId, webSocket);

            Interlocked.Increment(ref clientCount);

            try
            {
                TrimmedMemoryOwner? firstMessageJson;

                CancellationTokenSource fmCts = new CancellationTokenSource(5000);
                try
                {
                    firstMessageJson = await context.Receiver.ReceiveAsync(context.Token);

                    if (firstMessageJson == null || firstMessageJson.Memory.IsEmpty)
                    {
                        webSocket.Abort();
                        return;
                    }
                }
                catch
                {
                    webSocket.Abort();
                    return;
                }
                finally
                {
                    fmCts.Dispose();
                }


                var initMessage = new ConnectionInitMessage(firstMessageJson);

                if (initMessage.Type != MessageType.connection_init)
                {
                    webSocket.Abort();
                    return;
                }

                await context.Sender.SendAsync(new ConnectionAckMessage(initMessage.Id, context.ConnectionId));

                var lastPingId = Guid.Empty;

                if (options.WebsocketRequiresPing)
                {
                    context.EnableHeartbeatWatcher();
                }

                while (webSocket.State == WebSocketState.Open)
                {
                    TrimmedMemoryOwner? tmo;

                    try
                    {
                        tmo = await context.Receiver.ReceiveAsync(context.Token);

                        if (tmo == null || tmo.Memory.IsEmpty)
                        {
                            return;
                        }
                    }
                    catch
                    {
                        webSocket.Abort();
                        return;
                    }

                    if (options.CheckTokenExpirationOnMsgReceived && lastTokenExpirationDate > 0 &&
                        lastTokenExpirationDate < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                    {
                        webSocket.Abort();
                        return;
                    }

                    var message = new BaseMessage(tmo);

                    if (message.Id == Guid.Empty || !Guid.TryParse(message.ConnectionId, out _))
                    {
                        webSocket.Abort();
                        return;
                    }

                    if (message.ConnectionId != context.ConnectionId)
                        continue;

                    switch (message.Type)
                    {
                        case MessageType.ping:

                            await context.RateLimiter.TryAcquireAsync(context.ConnectionId, MessageType.ping,
                                message.Id, 0, context.Token);

                            if (!options.WebsocketRequiresPing)
                            {
                                break;
                            }

                            _ = HandlePing(lastPingId, new PingMessage(message), context);
                            break;

                        case MessageType.stream_init:

                            await context.RateLimiter.TryAcquireAsync(context.ConnectionId, MessageType.stream_init,
                                message.Id, 0, context.Token);

                            if (!options.WebSocketStreamIsAllowed)
                            {
                                await HandleError(message.Id, HubconResponse.Unauthorized(), context);
                                break;
                            }

                            _ = HandleStream(new StreamInitMessage(message), context);

                            break;

                        case MessageType.ack:

                            await context.RateLimiter.TryAcquireAsync(context.ConnectionId, MessageType.ack, message.Id,
                                0, context.Token);

                            if (!options.MessageRetryIsEnabled)
                            {
                                await HandleError(message.Id, HubconResponse.Unauthorized(), context);
                                break;
                            }

                            _ = HandleAck(new AckMessage(message), context);

                            break;

                        case MessageType.operation_invoke:
                            await context.RateLimiter.TryAcquireAsync(context.ConnectionId,
                                MessageType.operation_invoke, message.Id, 0, context.Token);

                            if (!options.WebSocketMethodsIsAllowed)
                            {
                                await HandleError(message.Id, HubconResponse.Unauthorized(), context);
                                break;
                            }

                            _ = HandleOperationInvoke(new OperationInvokeMessage(message), context);

                            break;

                        case MessageType.operation_call:
                            await context.RateLimiter.TryAcquireAsync(context.ConnectionId, MessageType.operation_call,
                                message.Id, 0, context.Token);

                            if (!options.WebSocketMethodsIsAllowed)
                            {
                                await HandleError(message.Id, HubconResponse.Unauthorized(), context);
                                break;
                            }

                            _ = HandleOperationCall(new OperationCallMessage(message), context);

                            break;

                        case MessageType.ingest_init:
                            await context.RateLimiter.TryAcquireAsync(context.ConnectionId, MessageType.ingest_init,
                                message.Id, 0, context.Token);

                            if (!options.WebSocketIngestIsAllowed)
                            {
                                await HandleError(message.Id, HubconResponse.Unauthorized(), context);
                                break;
                            }

                            _ = HandleIngestInit(new IngestInitMessage(message), context);

                            break;

                        case MessageType.ingest_data:
                            await context.RateLimiter.TryAcquireAsync(context.ConnectionId, MessageType.ingest_data,
                                message.Id, 0, context.Token);

                            if (!options.WebSocketIngestIsAllowed)
                            {
                                await HandleError(message.Id, HubconResponse.Unauthorized(), context);
                                break;
                            }

                            _ = HandleIngestData(new IngestDataMessage(message), context);

                            break;

                        case MessageType.ingest_data_with_ack:
                            await context.RateLimiter.TryAcquireAsync(context.ConnectionId,
                                MessageType.ingest_data_with_ack, message.Id, 0, context.Token);

                            if (!options.WebSocketIngestIsAllowed)
                            {
                                await HandleError(message.Id, HubconResponse.Unauthorized(), context);
                                break;
                            }

                            _ = HandleIngestDataWithAck(new IngestDataWithAckMessage(message), context);

                            break;

                        case MessageType.ingest_complete:
                            await context.RateLimiter.TryAcquireAsync(context.ConnectionId, MessageType.ingest_complete,
                                message.Id, 0, context.Token);

                            if (!options.WebSocketIngestIsAllowed)
                            {
                                await HandleError(message.Id, HubconResponse.Unauthorized(), context);
                                break;
                            }

                            _ = HandleIngestComplete(new IngestCompleteMessage(message), context);

                            break;
                        case MessageType.cancel:
                            await context.RateLimiter.TryAcquireAsync(context.ConnectionId, MessageType.cancel,
                                message.Id, 0, context.Token);

                            if (!options.RemoteCancellationIsAllowed)
                            {
                                break;
                            }

                            _ = CancelTask(message.Id, context);

                            break;
                        case MessageType.token_update:
                            await context.RateLimiter.TryAcquireAsync(context.ConnectionId, MessageType.token_update,
                                message.Id, 0, context.Token);

                            _ = HandleTokenRefresh(new TokenUpdateMessage(message), context);

                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                context.Logger.LogError(ex, "Critical error in Hubcon WebSocket Transport. Connection aborted.");
            }
            finally
            {
                try
                {
                    if (webSocket.State == WebSocketState.Open)
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnected",
                            CancellationToken.None);
                }
                catch
                {
                    // Ignored
                }

                try
                {
                    await context.DisposeAsync();
                }
                catch
                {
                    // Ignored
                }

                Interlocked.Decrement(ref clientCount);
            }
        }

        static async ValueTask<(ClaimsPrincipal ClaimsPrincipal, long ExpirationTime, string? AccessToken)?>
            IsAuthorized(HttpContext context, IInternalServerOptions options)
        {
            if (options.WebsocketRequiresAuthorization)
            {
                var token = context.Request.Query["access_token"];
                context.Request.Headers.Authorization = token;

                var authProvider =
                    options.AuthHandlerTypes.TryGetValue(HubconTransportAttribute.GetDefault<WebSocketTransport>(),
                        out var authHandlerType)
                        ? authHandlerType
                        : typeof(JwtAuthHandler);

                if (string.IsNullOrWhiteSpace(token))
                {
                    return null;
                }

                if (context.RequestServices.GetService(authProvider) is IAuthHandler provider)
                {
                    try
                    {
                        var operationContext = new OperationContext()
                        {
                            RequestServices = context.RequestServices,
                            HttpContext = context,
                            RequestAborted = context.RequestAborted,
                            IsTransportCalled = true
                        };

                        var claimsPrincipal = await Task.Run(async () =>
                        {
                            try
                            {
                                OperationContextProvider.SetContext(operationContext);
                                return await provider.AuthenticateAsync(operationContext, null!);
                            }
                            finally
                            {
                                OperationContextProvider.ClearContext();
                            }
                        });

                        if (claimsPrincipal is null)
                        {
                            return null;
                        }

                        var exp = claimsPrincipal.FindFirst("exp");

                        if (exp is null)
                            return null;

                        return (claimsPrincipal, long.Parse(exp.Value), token);
                    }
                    catch (Exception)
                    {
                        return null;
                    }
                }

                return null;
            }

            return (new ClaimsPrincipal(), long.MaxValue, null);
        }

        private static async Task CancelTask(Guid id, ClientWebSocketContext context)
        {
            if (context.ConnectionIsClosed) return;
            
            if (!context.Tasks.TryRemove(id, out var task)) return;
            await task.CancelAsync();
        }

        private static async Task HandleIngestComplete(IngestCompleteMessage ingestCompleteMessage,
            ClientWebSocketContext context)
        {
            try
            {
                if (context.ConnectionIsClosed) return;

                if (!await context.RateLimiter.TryAcquireAsync(context.ConnectionId, MessageType.ingest_complete,
                        ingestCompleteMessage.Id))
                {
                    await HandleError(ingestCompleteMessage.Id, HubconResponse.TooManyRequests(), context);
                    return;
                }

                if (ingestCompleteMessage.StreamIds == null)
                {
                    await HandleError(ingestCompleteMessage.Id, HubconResponse.BadRequest("StreamIds cannot be null"),
                        context);
                    return;
                }

                foreach (var id in ingestCompleteMessage.StreamIds)
                {
                    context.IngestRouters.TryRemove(id, out var complete);

                    if (complete.Item2 != null)
                    {
                        try
                        {
                            await complete.Item2.CancelAsync();
                            complete.Item1.OnCompleted();
                        }
                        catch
                        {
                            // Ignored
                        }
                    }

                    if (complete.Item4 != null)
                    {
                        try
                        {
                            await complete.Item4.RateBucket.DisposeAsync();
                        }
                        catch
                        {
                            // Ignored
                        }
                    }

                    if (complete.Item3 != null)
                    {
                        try
                        {
                            await complete.Item3.DisposeAsync();
                        }
                        catch
                        {
                            // Ignored
                        }
                    }
                }
            }
            finally
            {
                ingestCompleteMessage.Dispose();
            }
        }

        private static async Task HandleIngestDataWithAck(IngestDataWithAckMessage ingestDataWithAckMessage,
            ClientWebSocketContext context)
        {
            try
            {
                if (context.ConnectionIsClosed) return;

                if (!context.IngestRouters.TryGetValue(ingestDataWithAckMessage.Id, out var ingestWithAck))
                    return;

                if (!await context.RateLimiter.TryAcquireAsync(context.ConnectionId, MessageType.ingest_data_with_ack,
                        ingestDataWithAckMessage.Id))
                {
                    await HandleError(ingestDataWithAckMessage.Id, HubconResponse.TooManyRequests(), context);
                    return;
                }

                ingestWithAck.Item3.NotifyHeartbeat();
                ingestWithAck.Item1.OnNextObject(ingestDataWithAckMessage.Data);

                var ingestDataAckMessage = new IngestDataAckMessage(ingestDataWithAckMessage.Id, context.ConnectionId);
                await context.Sender.SendAsync(ingestDataAckMessage);
            }
            finally
            {
                ingestDataWithAckMessage.Dispose();
            }
        }

        private static async Task HandleIngestData(IngestDataMessage ingestDataMessage, ClientWebSocketContext context)
        {
            try
            {
                if (context.ConnectionIsClosed) return;

                if (!context.IngestRouters.TryGetValue(ingestDataMessage.Id, out var ingest))
                    return;

                if (!await context.RateLimiter.TryAcquireAsync(context.ConnectionId, MessageType.ingest_data,
                        ingestDataMessage.Id))
                {
                    await HandleError(ingestDataMessage.Id, HubconResponse.TooManyRequests(), context);
                    return;
                }

                ingest.Item3.NotifyHeartbeat();
                ingest.Item1.OnNextElement(ingestDataMessage.Data);
            }
            finally
            {
                ingestDataMessage.Dispose();
            }
        }

        private static async Task HandleIngestInit(IngestInitMessage ingestInitMessage, ClientWebSocketContext context)
        {
            List<HeartbeatWatcher> watchers = null!;

            try
            {
                if (context.ConnectionIsClosed) return;

                Dictionary<Guid, object> sources = new();
                watchers = new();

                using var localCts = CancellationTokenSource.CreateLinkedTokenSource(context.Token);

                var operationRequest = context.Converter.DeserializeData<OperationRequest>(ingestInitMessage.Payload);

                if (!context.OperationRegistry.TryGetOperationBlueprint(operationRequest,
                        HubconTransportAttribute.GetDefault<WebSocketTransport>(), out var blueprint))
                    return;

                if (!await context.RateLimiter.TryAcquireAsync(context.ConnectionId, MessageType.ingest_init,
                        ingestInitMessage.Id, 1, CancellationToken.None))
                {
                    await HandleError(ingestInitMessage.Id, HubconResponse.TooManyRequests(), context);
                    return;
                }

                bool shareLimiter = blueprint!.Attributes.Any(x => x is IngestShareLimiter);
                RateLimitAttribute? sharedSettings = null;
                if (shareLimiter)
                    sharedSettings = context.SettingsManager.GetSettings(operationRequest,
                        HubconTransportAttribute.GetDefault<WebSocketTransport>(), () => new RateLimitAttribute());

                context.IngestHandlers.TryAdd(ingestInitMessage.Id, localCts);

                foreach (var id in ingestInitMessage.StreamIds)
                {
                    RateLimitAttribute settings = sharedSettings ?? context.SettingsManager.GetSettings(
                        operationRequest,
                        HubconTransportAttribute.GetDefault<WebSocketTransport>(), () => new RateLimitAttribute());

                    if (context.IngestRouters.TryGetValue(id, out _))
                        return;

                    var observable = new GenericObservable<JsonElement>(context.Converter);

                    var bufferOptions = new BoundedChannelOptions(settings.QueueLimit)
                    {
                        FullMode = BoundedChannelFullMode.Wait,
                        Capacity = settings.QueueLimit,
                        SingleReader = true,
                        SingleWriter = false,
                        AllowSynchronousContinuations = false,
                    };

                    var observer = AsyncObserver.Create<JsonElement>(context.Converter, bufferOptions);
                    observable.Subscribe(observer);

                    var hw = new HeartbeatWatcher(context.InternalServerOptions.IngestTimeout, async () =>
                    {
                        observable.OnCompleted();
                        context.IngestRouters.TryRemove(id, out var complete);

                        if (complete.Item2 != null)
                        {
                            try
                            {
                                await complete.Item2.CancelAsync();
                                complete.Item2.Dispose();
                            }
                            catch
                            {
                                // Ignored
                            }
                        }

                        if (complete.Item4 != null)
                        {
                            try
                            {
                                await complete.Item4.RateBucket.DisposeAsync();
                            }
                            catch
                            {
                                // Ignored
                            }
                        }

                        await context.RateLimiter.Unlink(context.ConnectionId, id);
                    });

                    watchers.Add(hw);
                    await context.RateLimiter.Link(context.ConnectionId, id,
                        HubconTransportAttribute.GetDefault<WebSocketTransport>(), operationRequest);
                    context.IngestRouters.TryAdd(id, (observable, localCts, hw, settings));
                    sources.TryAdd(id, observer.GetAsyncEnumerable());
                }

                await using var scope = context.CreateAsyncScope();

                var ingestTask = DefaultEntrypoint.HandleIngest(
                    operationRequest,
                    HubconTransportAttribute.GetDefault<WebSocketTransport>(),
                    scope.ServiceProvider,
                    sources,
                    null,
                    localCts.Token);

                await context.Sender.SendAsync(new IngestInitAckMessage(ingestInitMessage.Id, context.ConnectionId));
                var result = await ingestTask;

                if (context.Sender.State != WebSocketState.Open)
                    return;

                if (result.Failure)
                {
                    await HandleError(ingestInitMessage.Id, result, context);
                    return;
                }

                await context.Sender.SendAsync(new IngestResultMessage(ingestInitMessage.Id, context.ConnectionId,
                    context.Converter.SerializeToElement(result)));
            }
            catch (Exception ex)
            {
                context.Logger.LogError(ex.Message);

                if (context.Sender.State != WebSocketState.Open)
                    return;

                await context.Sender.SendAsync(new IngestResultMessage(ingestInitMessage.Id, context.ConnectionId,
                    context.Converter.SerializeToElement(ex.Message)));
            }
            finally
            {
                try
                {
                    if (watchers != null)
                    {
                        foreach (var watcher in watchers)
                        {
                            try
                            {
                                await watcher.DisposeAsync();
                            }
                            catch
                            {
                                // Ignored
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    context.Logger.LogError(ex.Message);
                }
                finally
                {
                    watchers?.Clear();
                }

                context.IngestHandlers.TryRemove(ingestInitMessage.Id, out _);
                ingestInitMessage.Dispose();
            }
        }

        private static async Task HandleOperationInvoke(OperationInvokeMessage operationInvokeMessage,
            ClientWebSocketContext context)
        {
            try
            {
                if (context.ConnectionIsClosed) return;

                using var localCts = CancellationTokenSource.CreateLinkedTokenSource(context.Token);

                if (!context.Tasks.TryAdd(operationInvokeMessage.Id, localCts))
                    return;

                if (!await context.RateLimiter.TryAcquireAsync(context.ConnectionId, MessageType.operation_invoke,
                        operationInvokeMessage.Id, 1, CancellationToken.None))
                {
                    await HandleError(operationInvokeMessage.Id, HubconResponse.TooManyRequests(), context);
                    return;
                }

                IOperationRequest operationRequest =
                    context.Converter.DeserializeData<OperationRequest>(operationInvokeMessage.Payload);

                await using var scope = context.HttpContext.RequestServices.CreateAsyncScope();

                var response = await DefaultEntrypoint.HandleMethodWithResult(
                    operationRequest,
                    HubconTransportAttribute.GetDefault<WebSocketTransport>(),
                    scope.ServiceProvider,
                    null,
                    localCts.Token);

                if (!context.ConnectionIsClosed)
                {
                    var message = new OperationResponseMessage(
                        operationInvokeMessage.Id,
                        context.ConnectionId,
                        context.Converter.SerializeToElement(response)
                    );

                    await context.Sender.SendAsync(message);
                }
            }
            finally
            {
                context.Tasks.TryRemove(operationInvokeMessage.Id, out _);
                operationInvokeMessage.Dispose();
            }
        }

        private static async Task HandleOperationCall(OperationCallMessage operationCallMessage,
            ClientWebSocketContext context)
        {
            try
            {
                if (context.ConnectionIsClosed) return;

                using var localCts = context.GetLinkedCancellationTokenSource();

                if (!context.Tasks.TryAdd(operationCallMessage.Id, localCts))
                    return;

                if (!await context.RateLimiter.TryAcquireAsync(context.ConnectionId, MessageType.operation_call,
                        operationCallMessage.Id, 1, CancellationToken.None))
                {
                    await HandleError(operationCallMessage.Id, HubconResponse.TooManyRequests(), context);
                    return;
                }

                IOperationRequest operationRequest =
                    context.Converter.DeserializeData<OperationRequest>(operationCallMessage.Payload);

                await using var scope = context.CreateAsyncScope();

                var response = await DefaultEntrypoint.HandleMethodVoid(
                    operationRequest,
                    HubconTransportAttribute.GetDefault<WebSocketTransport>(),
                    scope.ServiceProvider,
                    null,
                    localCts.Token);

                if (response.Failure)
                {
                    context.Logger.LogError(response.Message);
                }
            }
            catch (Exception ex)
            {
                context.Logger.LogError("{}", ex.Message);
            }
            finally
            {
                context.Tasks.TryRemove(operationCallMessage.Id, out _);
                operationCallMessage.Dispose();
            }
        }

        private static async Task HandleAck(AckMessage ackMessage, ClientWebSocketContext context)
        {
            try
            {
                if (context.ConnectionIsClosed) return;

                if (context.AckChannels.TryGetValue(ackMessage.Id, out IRetryableMessage? value))
                {
                    await value.AckAsync();

                    context.AckChannels.TryRemove(ackMessage.Id, out _);

                    if (!await context.RateLimiter.TryAcquireAsync(context.ConnectionId, MessageType.ack,
                            ackMessage.Id))
                    {
                        await HandleError(ackMessage.Id, HubconResponse.TooManyRequests(), context);
                    }
                }
            }
            finally
            {
                ackMessage.Dispose();
            }
        }

        private static async Task HandleStream(StreamInitMessage streamInitMessage, ClientWebSocketContext context)
        {
            try
            {
                if (context.ConnectionIsClosed) return;

                using var localCts = context.GetLinkedCancellationTokenSource();

                if (streamInitMessage.Id == Guid.Empty) return;

                if (!await context.RateLimiter.TryAcquireAsync(context.ConnectionId, MessageType.stream_init,
                        streamInitMessage.Id, 1, CancellationToken.None))
                {
                    await HandleError(streamInitMessage.Id, HubconResponse.TooManyRequests(), context);
                    return;
                }

                if (context.Streams.ContainsKey(streamInitMessage.Id)) return;

                context.Streams.TryAdd(streamInitMessage.Id, localCts);

                IOperationRequest operationRequest =
                    context.Converter.DeserializeData<OperationRequest>(streamInitMessage.Payload);

                await using var scope = context.HttpContext.RequestServices.CreateAsyncScope();

                var streamResult = await DefaultEntrypoint.HandleMethodStream(
                    operationRequest,
                    HubconTransportAttribute.GetDefault<WebSocketTransport>(),
                    scope.ServiceProvider,
                    null,
                    localCts.Token);

                if (streamResult.Failure)
                {
                    await HandleError(streamInitMessage.Id, HubconResponse.Unauthorized(), context);
                    return;
                }

                await context.RateLimiter.Link(context.ConnectionId, streamInitMessage.Id,
                    HubconTransportAttribute.GetDefault<WebSocketTransport>(), operationRequest);

                var stream = streamResult.Data as IAsyncEnumerable<object?>;

                await foreach (var item in stream!.WithCancellation(localCts.Token))
                {
                    await context.RateLimiter.TryAcquireAsync(context.ConnectionId, MessageType.stream_init,
                        streamInitMessage.Id, 0, context.Token);
                    await context.RateLimiter.TryAcquireAsync(context.ConnectionId, MessageType.stream_init,
                        streamInitMessage.Id, 1, CancellationToken.None);

                    if (item != null && item.GetType().IsAssignableTo(typeof(IRetryableMessage)))
                    {
                        IRetryableMessage? retryable = item as IRetryableMessage;
                        var ackId = Guid.NewGuid();
                        context.AckChannels.TryAdd(ackId, retryable!);

                        while (await retryable!.CanRetry() && !localCts.IsCancellationRequested)
                        {
                            retryable.GetPayload(out object? message);
                            var streamMessage = new StreamDataWithAckMessage(streamInitMessage.Id, context.ConnectionId,
                                context.Converter.SerializeToElement(message), ackId);
                            await context.Sender.SendAsync(streamMessage);

                            if (!context.InternalServerOptions.MessageRetryIsEnabled)
                            {
                                break;
                            }
                        }

                        if (context.AckChannels.TryRemove(ackId, out IRetryableMessage? channel))
                            await channel.AckAsync();
                    }
                    else
                    {
                        if (!localCts.IsCancellationRequested)
                        {
                            var response = new StreamDataMessage(
                                streamInitMessage.Id,
                                context.ConnectionId,
                                context.Converter.SerializeToElement(item)
                            );

                            await context.Sender.SendAsync(response);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Ignored
            }
            catch (Exception)
            {
                if (!context.ConnectionIsClosed)
                {
                    await HandleError(streamInitMessage.Id, HubconResponse.InternalError<string>(), context);
                }
            }
            finally
            {
                context.Streams.TryRemove(streamInitMessage.Id, out _);

                if (!context.ConnectionIsClosed)
                {
                    await context.Sender.SendAsync(
                        new StreamCompleteMessage(streamInitMessage.Id, context.ConnectionId));
                }

                streamInitMessage.Dispose();

                await context.RateLimiter.Unlink(context.ConnectionId, streamInitMessage.Id);
            }
        }

        private static async Task HandleTokenRefresh(TokenUpdateMessage tokenUpdateMessage,
            ClientWebSocketContext context)
        {
            if (context.ConnectionIsClosed) return;

            using var localCts = CancellationTokenSource.CreateLinkedTokenSource(context.Token);

            if (!await context.RateLimiter.TryAcquireAsync(context.ConnectionId, MessageType.token_update,
                    tokenUpdateMessage.Id, 1, CancellationToken.None))
            {
                await HandleError(tokenUpdateMessage.Id, HubconResponse.TooManyRequests(), context);
                return;
            }

            var user = await IsAuthorized(context.HttpContext, context.InternalServerOptions);

            try
            {
                if (!context.Tasks.TryAdd(tokenUpdateMessage.Id, localCts))
                    return;

                if (user is null)
                {
                    await context.Sender.SendAsync(new TokenUpdateResponseMessage(tokenUpdateMessage.Id,
                        context.ConnectionId, false,
                        "Token refresh failed."));
                    await context.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Unauthorized");
                    context.Logger.LogInformation("Websocket re-authentication failed.");
                    return;
                }

                context.HttpContext.Request.Headers.Authorization = tokenUpdateMessage.Token;
                context.HttpContext.User = user.Value.ClaimsPrincipal;
                context.Supervisor.UpdateExpiration(context.ConnectionId, user.Value.ExpirationTime);
                await context.Sender.SendAsync(new TokenUpdateResponseMessage(tokenUpdateMessage.Id,
                    context.ConnectionId, true,
                    "Token refresh OK."));
            }
            catch (OperationCanceledException)
            {
                await context.Sender.SendAsync(new TokenUpdateResponseMessage(tokenUpdateMessage.Id,
                    context.ConnectionId, false,
                    "Operation cancelled."));
                await context.CloseAsync(WebSocketCloseStatus.InternalServerError, "Operation cancelled.");
                context.Logger.LogInformation("Token refresh update: Operation cancelled.");
            }
            catch (Exception ex)
            {
                await context.Sender.SendAsync(new TokenUpdateResponseMessage(tokenUpdateMessage.Id,
                    context.ConnectionId, false, "Internal server error."));
                await context.CloseAsync(WebSocketCloseStatus.InternalServerError, "Internal server error.");
                context.Logger.LogError(ex.Message);
            }
            finally
            {
                context.Tasks.TryRemove(tokenUpdateMessage.Id, out _);
                tokenUpdateMessage.Dispose();
            }
        }

        private static async Task HandleError(Guid id, IResponse error, ClientWebSocketContext context)
        {
            if (context.ConnectionIsClosed)
                return;

            var localMessage = new ErrorMessage(id, context.ConnectionId, null!);

            localMessage.Error = context.Converter.Serialize(new HubconResponse<string>(
                error.Success,
                error.Failure,
                error.Message,
                error.Error,
                error.StatusCode,
                null!));

            await context.Sender.SendAsync(localMessage);
        }

        private static async Task HandlePing(Guid lastPingId, PingMessage pingMessage, ClientWebSocketContext context)
        {
            try
            {
                if (context.ConnectionIsClosed) return;

                if (lastPingId == pingMessage.Id)
                {
                    await context.CloseAsync(WebSocketCloseStatus.InvalidPayloadData, "Ping error");
                    return;
                }

                if (!await context.RateLimiter.TryAcquireAsync(context.ConnectionId, MessageType.ping, pingMessage.Id,
                        1,
                        CancellationToken.None))
                {
                    await HandleError(pingMessage.Id, HubconResponse.TooManyRequests(), context);
                    return;
                }

                context.Watcher?.NotifyHeartbeat();
                await context.Sender.SendAsync(new PongMessage(pingMessage.Id, context.ConnectionId));
            }
            finally
            {
                pingMessage.Dispose();
            }
        }
    }
}