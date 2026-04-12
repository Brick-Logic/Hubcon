using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Core.Configuration;
using Hubcon.Server.Core.Entrypoint;
using Hubcon.Server.Core.Pipelines;
using Hubcon.Server.Core.Pipelines.UpgradedPipeline;
using Hubcon.Server.Core.Websockets.Helpers;
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
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Channels;

namespace Hubcon.Server.Core.Websockets.Middleware
{
    /// <summary>
    /// The hubcon websocket middleware, used to handle hubcon websocket connections.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class HubconWebSocketMiddleware
    {
        private readonly RequestDelegate next;
        private readonly IDynamicConverter converter;
        private readonly IOperationRegistry operationRegistry;
        private readonly ILogger<HubconWebSocketMiddleware> logger;
        private readonly IConnectionSupervisor connectionSupervisor;
        private readonly IInternalServerOptions options;
        int clientCount = 0;

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="next"></param>
        /// <param name="converter"></param>
        /// <param name="operationRegistry"></param>
        /// <param name="logger"></param>
        /// <param name="connectionSupervisor"></param>
        /// <param name="options"></param>
        /// <param name="telemetryProvider"></param>
        public HubconWebSocketMiddleware(
            RequestDelegate next,
            IDynamicConverter converter,
            IOperationRegistry operationRegistry,
            ILogger<HubconWebSocketMiddleware> logger,
            IConnectionSupervisor connectionSupervisor,
            IInternalServerOptions options,
            ITelemetryProvider telemetryProvider)
        {
            this.next = next;
            this.converter = converter;
            this.operationRegistry = operationRegistry;
            this.logger = logger;
            this.connectionSupervisor = connectionSupervisor;
            this.options = options;

            telemetryProvider.RegisterProvider(x => x.GetCurrentWebsocketClients, () => clientCount);
        }

        /// <summary>
        /// Begins the execution of the pipeline.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="serviceProvider"></param>
        /// <returns></returns>
        public async Task InvokeAsync(HttpContext context, IServiceProvider serviceProvider)
        {
            if (!context.WebSockets.IsWebSocketRequest || !(context.Request.Path == options.WebSocketPathPrefix))
            {
                await next(context);
                return;
            }

            var corsService = context.RequestServices.GetRequiredService<ICorsService>();
            var corsPolicyProvider = context.RequestServices.GetRequiredService<ICorsPolicyProvider>();

            var policy = await corsPolicyProvider.GetPolicyAsync(context, null);

            if (policy != null)
            {
                var corsResult = corsService.EvaluatePolicy(context, policy);

                if (!corsResult.IsOriginAllowed)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return;
                }

                corsService.ApplyResult(corsResult, context.Response);
            }

            long lastTokenExpirationDate = 0;
            string connectionId = Guid.NewGuid().ToString();
            WebSocket webSocket = null!;

            var userData = await IsAuthorized(context);

            if (userData != null)
            {
                context.Request.Headers.Authorization = userData.Value.AccessToken;
                context.User = userData.Value.ClaimsPrincipal;
                lastTokenExpirationDate = userData.Value.ExpirationTime;
                connectionSupervisor.Register(connectionId, userData.Value.ExpirationTime, async () =>
                {
                    webSocket.Abort();
                });
            }
            else
            {
                return;
            }

            try
            {
                webSocket = await context.WebSockets.AcceptWebSocketAsync();
            }
            catch (Exception)
            {
                return;
            }

            Interlocked.Increment(ref clientCount);

            IOperationConfigRegistry operationConfigRegistry = context.RequestServices.GetRequiredService<IOperationConfigRegistry>();
            IGlobalRateLimiterManager rateLimiterManager = context.RequestServices.GetRequiredService<IGlobalRateLimiterManager>();

            TimeSpan timeoutSeconds = options.WebSocketTimeout;
            HeartbeatWatcher _heartbeatWatcher = null!;
            CancellationTokenSource cts = new();
            ConcurrentDictionary<Guid, CancellationTokenSource> _subscriptions = null!;
            ConcurrentDictionary<Guid, CancellationTokenSource> _streams = null!;
            ConcurrentDictionary<Guid, (BaseObservable, CancellationTokenSource, HeartbeatWatcher, RateLimitAttribute)> _ingestRouters = null!;
            ConcurrentDictionary<Guid, (CancellationTokenSource, CancellationTokenRegistration)> _ingestHandlers = null!;
            ConcurrentDictionary<Guid, IRetryableMessage> _ackChannels = null!;
            ConcurrentDictionary<Guid, CancellationTokenSource> _tasks = null!;


            SettingsManager settingsManager = new SettingsManager(operationRegistry, operationConfigRegistry);
            WebSocketMessageSender sender = null!;
            WebSocketMessageReceiver receiver = null!;

            try
            {
                receiver = new WebSocketMessageReceiver(webSocket, options);
                sender = new WebSocketMessageSender(webSocket, converter);
                TrimmedMemoryOwner? firstMessageJson = null;

                CancellationTokenSource fmCts = new CancellationTokenSource(5000);
                try
                {
                    firstMessageJson = await receiver.ReceiveAsync(fmCts.Token);

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

                if (initMessage == null || initMessage.Type != MessageType.connection_init)
                {
                    webSocket.Abort();
                    return;
                }

                await sender.SendAsync(new ConnectionAckMessage(initMessage.Id, connectionId));

                var lastPingId = Guid.Empty;

                _heartbeatWatcher = new HeartbeatWatcher(timeoutSeconds, () =>
                {
                    webSocket.Abort();
                    return cts.CancelAsync();
                });

                _subscriptions = new();
                _streams = new();
                _ingestRouters = new();
                _ingestHandlers = new();
                _ackChannels = new();
                _tasks = new();

                while (webSocket.State == WebSocketState.Open)
                {
                    TrimmedMemoryOwner? tmo;

                    try
                    {
                        tmo = await receiver.ReceiveAsync();

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

                    if (options.CheckTokenExpirationOnMsgReceived && lastTokenExpirationDate > 0 && lastTokenExpirationDate < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                    {
                        webSocket.Abort();
                        return;
                    }

                    var message = new BaseMessage(tmo);

                    if (message.Id == Guid.Empty 
                        || message.ConnectionId.Length != 36 
                        || message.ConnectionId[8] != '-' 
                        || message.ConnectionId[13] != '-' 
                        || message.ConnectionId[18] != '-' 
                        || message.ConnectionId[23] != '-')
                    {
                        webSocket.Abort();
                        return;
                    }

                    if (message.ConnectionId != connectionId)
                        continue;

                    switch (message.Type)
                    {
                        case MessageType.ping:

                            await rateLimiterManager.TryAcquireAsync(connectionId, MessageType.ping, message.Id, 0);

                            if (!options.WebsocketRequiresPing)
                            {
                                break;
                            }

                            _ = HandlePing(webSocket, connectionId, sender, lastPingId, _heartbeatWatcher, new PingMessage(message), rateLimiterManager);
                            break;

                        case MessageType.stream_init:

                            await rateLimiterManager.TryAcquireAsync(connectionId, MessageType.stream_init, message.Id, 0);

                            if (!options.WebSocketSubscriptionIsAllowed)
                            {
                                await HandleError(message.Id, HubconResponse.Unauthorized());
                                break;
                            }

                            _ = HandleStream(
                                context,
                                connectionId,
                                _streams,
                                _ackChannels,
                                sender,
                                new StreamInitMessage(message),
                                webSocket,
                                rateLimiterManager,
                                cts.Token);

                            break;

                        case MessageType.ack:

                            await rateLimiterManager.TryAcquireAsync(connectionId, MessageType.ack, message.Id, 0);

                            if (!options.MessageRetryIsEnabled)
                            {
                                await HandleError(message.Id, HubconResponse.Unauthorized());
                                break;
                            }

                            _ = HandleAck(
                                _ackChannels,
                                connectionId,
                                new AckMessage(message),
                                rateLimiterManager);

                            break;

                        case MessageType.operation_invoke:

                            await rateLimiterManager.TryAcquireAsync(connectionId, MessageType.operation_invoke, message.Id, 0);

                            if (!options.WebSocketMethodsIsAllowed)
                            {
                                await HandleError(message.Id, HubconResponse.Unauthorized());
                                break;
                            }

                            _ = HandleOperationInvoke(
                                context,
                                connectionId,
                                sender,
                                new OperationInvokeMessage(message),
                                _tasks,
                                webSocket,
                                rateLimiterManager,
                                cts.Token);

                            break;

                        case MessageType.operation_call:

                            await rateLimiterManager.TryAcquireAsync(connectionId, MessageType.operation_call, message.Id, 0);

                            if (!options.WebSocketMethodsIsAllowed)
                            {
                                await HandleError(message.Id, HubconResponse.Unauthorized());
                                break;
                            }

                            _ = HandleOperationCall(
                                context,
                                connectionId,
                                new OperationCallMessage(message),
                                _tasks,
                                rateLimiterManager,
                                cts.Token);

                            break;

                        case MessageType.ingest_init:

                            await rateLimiterManager.TryAcquireAsync(connectionId, MessageType.ingest_init, message.Id, 0);

                            if (!options.WebSocketIngestIsAllowed)
                            {
                                await HandleError(message.Id, HubconResponse.Unauthorized());
                                break;
                            }

                            _ = HandleIngestInit(
                                context,
                                connectionId,
                                sender,
                                new IngestInitMessage(message),
                                _ingestHandlers,
                                _ingestRouters,
                                settingsManager,
                                operationConfigRegistry,
                                rateLimiterManager,
                                cts.Token);

                            break;

                        case MessageType.ingest_data:

                            await rateLimiterManager.TryAcquireAsync(connectionId, MessageType.ingest_data, message.Id, 0);

                            if (!options.WebSocketIngestIsAllowed)
                            {
                                await HandleError(message.Id, HubconResponse.Unauthorized());
                                break;
                            }

                            _ = HandleIngestData(_ingestRouters, connectionId, new IngestDataMessage(message), rateLimiterManager);

                            break;

                        case MessageType.ingest_data_with_ack:

                            await rateLimiterManager.TryAcquireAsync(connectionId, MessageType.ingest_data_with_ack, message.Id, 0);

                            if (!options.WebSocketIngestIsAllowed)
                            {
                                await HandleError(message.Id, HubconResponse.Unauthorized());
                                break;
                            }

                            _ = HandleIngestDataWithAck(_ingestRouters, connectionId, sender, new IngestDataWithAckMessage(message), rateLimiterManager);

                            break;

                        case MessageType.ingest_complete:

                            await rateLimiterManager.TryAcquireAsync(connectionId, MessageType.ingest_complete, message.Id, 0);

                            if (!options.WebSocketIngestIsAllowed)
                            {
                                await HandleError(message.Id, HubconResponse.Unauthorized());
                                break;
                            }

                            _ = HandleIngestComplete(_ingestRouters, connectionId, new IngestCompleteMessage(message), rateLimiterManager);

                            break;
                        case MessageType.cancel:

                            await rateLimiterManager.TryAcquireAsync(connectionId, MessageType.cancel, message.Id, 0);

                            if (!options.RemoteCancellationIsAllowed)
                            {
                                break;
                            }

                            _ = CancelTask(message.Id, connectionId, _tasks, rateLimiterManager);

                            break;
                        case MessageType.token_update:

                            await rateLimiterManager.TryAcquireAsync(connectionId, MessageType.token_update, message.Id, 0);

                            _ = HandleTokenRefresh(
                                context,
                                sender,
                                connectionId,
                                _tasks,
                                new TokenUpdateMessage(message),
                                webSocket,
                                rateLimiterManager,
                                cts.Token);

                            break;

                        default:
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Critical error in Hubcon WebSocket Transport. Connection aborted.");
            }
            finally
            {
                try
                {
                    if (webSocket.State == WebSocketState.Open)
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnected", CancellationToken.None);
                }
                finally
                {
                }
                connectionId = "";
                await connectionSupervisor.UnregisterAsync(connectionId);

                if (_heartbeatWatcher != null)
                    await _heartbeatWatcher.DisposeAsync();

                if (_subscriptions != null)
                {
                    foreach (var sub in _subscriptions)
                    {
                        if (_subscriptions.TryRemove(sub.Key, out var value))
                        {
                            if (value != null && !value.IsCancellationRequested)
                            {
                                value?.CancelAsync();
                                value?.Dispose();
                            }
                        }
                    }
                }

                if (_ackChannels != null)
                {
                    foreach (var channel in _ackChannels)
                    {
                        try
                        {
                            if (_ackChannels.TryRemove(channel.Key, out var value))
                            {
                                await value.FailedAckAsync();
                            }
                        }
                        catch
                        {
                        }
                    }
                }

                if (_ingestRouters != null)
                {
                    foreach (var task in _ingestRouters)
                    {
                        if (_ingestRouters.TryRemove(task.Key, out var value))
                        {
                            value.Item1?.OnCompleted();
                            await value.Item3.DisposeAsync();
                            if (value.Item2 != null && !value.Item2.IsCancellationRequested)
                            {
                                value.Item2?.CancelAsync();
                                value.Item2?.Dispose();
                            }
                            await value.Item4.RateBucket.DisposeAsync();
                        }
                    }
                }

                if (_ingestHandlers != null)
                {
                    foreach (var task in _ingestHandlers)
                    {
                        if (_ingestHandlers.TryRemove(task.Key, out var value))
                        {
                            await value.Item2.DisposeAsync();
                        }
                    }
                }

                if (_tasks != null)
                {
                    foreach (var task in _tasks)
                    {
                        _tasks.TryRemove(task.Key, out _);
                    }
                }

                await Task.Delay(500);
                webSocket.Dispose();

                Interlocked.Decrement(ref clientCount);
            }

            async ValueTask<(ClaimsPrincipal ClaimsPrincipal, long ExpirationTime, string AccessToken)?> IsAuthorized(HttpContext context)
            {
                if (options.WebsocketRequiresAuthorization)
                {
                    var token = context.Request.Query["access_token"];
                    context.Request.Headers.Authorization = token;

                    var authProvider = options.AuthHandlerTypes.TryGetValue(HubconTransportAttribute.GetDefault<WebSocketTransport>(), out Type? authHandlerType)
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
                                    return await provider.AuthenticateAsync(operationContext, default!)!;
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

                            long.TryParse(exp?.Value, out long longExpiration);
                            return (claimsPrincipal, longExpiration, token)!;
                        }
                        catch (Exception)
                        {
                            return null;
                        }
                    }
                    else
                    {
                        return null;
                    }
                }
                else
                {
                    var token = context.Request.Headers.Authorization = Guid.NewGuid().ToString("N");
                    return (new ClaimsPrincipal(), long.MaxValue, token)!;
                }
            }

            async Task CancelTask(Guid id, string connectionId, ConcurrentDictionary<Guid, CancellationTokenSource> tasks, IGlobalRateLimiterManager rateLimiterManager)
            {
                if (!tasks.TryRemove(id, out var task)) return;
                await task.CancelAsync();
            }

            async Task HandleIngestComplete(
                ConcurrentDictionary<Guid, (BaseObservable, CancellationTokenSource, HeartbeatWatcher, RateLimitAttribute)> _ingests,
                string connectionId,
                IngestCompleteMessage ingestCompleteMessage,
                IGlobalRateLimiterManager rateLimiterManager)
            {
                if(!await rateLimiterManager.TryAcquireAsync(connectionId, MessageType.ingest_complete, ingestCompleteMessage.Id, 1))
                {
                    await HandleError(ingestCompleteMessage.Id, HubconResponse.TooManyRequests());
                    return;
                }

                if (ingestCompleteMessage.StreamIds == null)
                {
                    await HandleError(ingestCompleteMessage.Id, HubconResponse.BadRequest("StreamIds cannot be null"));
                    return;
                }

                foreach (var id in ingestCompleteMessage.StreamIds)
                {
                    _ingests.TryRemove(id, out var complete);

                    complete.Item2?.CancelAsync();
                    complete.Item1?.OnCompleted();
                    complete.Item4?.RateBucket.Dispose();

                    if (complete.Item3 != null)
                        await complete.Item3.DisposeAsync();

                }

                ingestCompleteMessage.Dispose();
            }

            async Task HandleIngestDataWithAck(
                ConcurrentDictionary<Guid, (BaseObservable, CancellationTokenSource, HeartbeatWatcher, RateLimitAttribute)> _ingests,
                string connectionId,
                WebSocketMessageSender sender,
                IngestDataWithAckMessage ingestDataWithAckMessage
    ,
                IGlobalRateLimiterManager rateLimiterManager)
            {
                if (ingestDataWithAckMessage == null || !_ingests.TryGetValue(ingestDataWithAckMessage.Id, out var ingestWithAck))
                    return;

                if (!await rateLimiterManager.TryAcquireAsync(connectionId, MessageType.ingest_data_with_ack, ingestDataWithAckMessage.Id, 1))
                {
                    await HandleError(ingestDataWithAckMessage.Id, HubconResponse.TooManyRequests());
                    return;
                }

                ingestWithAck.Item3.NotifyHeartbeat();
                ingestWithAck.Item1.OnNextObject(ingestDataWithAckMessage.Data);

                var ingestDataAckMessage = new IngestDataAckMessage(ingestDataWithAckMessage.Id, connectionId);
                await sender.SendAsync(ingestDataAckMessage);
                ingestDataWithAckMessage.Dispose();
            }

            async Task HandleIngestData(
                ConcurrentDictionary<Guid, (BaseObservable, CancellationTokenSource, HeartbeatWatcher, RateLimitAttribute)> _ingests, 
                string connectionId, 
                IngestDataMessage ingestDataMessage, 
                IGlobalRateLimiterManager rateLimiterManager)
            {
                if (ingestDataMessage == null || !_ingests.TryGetValue(ingestDataMessage.Id, out var ingest))
                    return;

                if (!await rateLimiterManager.TryAcquireAsync(connectionId, MessageType.ingest_data, ingestDataMessage.Id, 1))
                {
                    await HandleError(ingestDataMessage.Id, HubconResponse.TooManyRequests());
                    return;
                }

                ingest.Item3.NotifyHeartbeat();
                ingest.Item1.OnNextElement(ingestDataMessage.Data);
                ingestDataMessage.Dispose();
            }

            async Task HandleIngestInit(
                HttpContext context,
                string connectionId,
                WebSocketMessageSender sender,
                IngestInitMessage ingestInitMessage,
                ConcurrentDictionary<Guid, (CancellationTokenSource, CancellationTokenRegistration)> _ingestHandlers,
                ConcurrentDictionary<Guid, (BaseObservable, CancellationTokenSource, HeartbeatWatcher, RateLimitAttribute)> _ingestRouters,
                ISettingsManager settingsManager,
                IOperationConfigRegistry operationConfigRegistry,
                IGlobalRateLimiterManager rateLimiterManager,
                CancellationToken cancellationToken)
            {
                Dictionary<Guid, object> sources = new();
                using var localCts = new CancellationTokenSource();
                using var registration = cancellationToken.Register(localCts.Cancel);

                List<HeartbeatWatcher> watchers = new();

                try
                {
                    var operationRequest = converter.DeserializeData<OperationRequest>(ingestInitMessage!.Payload)!;

                    if (!operationRegistry.TryGetOperationBlueprint(operationRequest, HubconTransportAttribute.GetDefault<WebSocketTransport>(), out var blueprint))
                        return;

                    if (!await rateLimiterManager.TryAcquireAsync(connectionId, MessageType.ingest_init, ingestInitMessage.Id, 1))
                    {
                        await HandleError(ingestInitMessage.Id, HubconResponse.TooManyRequests());
                        return;
                    }

                    bool shareLimiter = blueprint!.Attributes.Any(x => x is IngestShareLimiter);
                    RateLimitAttribute? sharedSettings = null;
                    if (shareLimiter) sharedSettings = settingsManager.GetSettings(operationRequest, HubconTransportAttribute.GetDefault<WebSocketTransport>(), () => new RateLimitAttribute()); ;

                    _ingestHandlers.TryAdd(ingestInitMessage.Id, (localCts, registration));

                    foreach (var id in ingestInitMessage!.StreamIds)
                    {
                        RateLimitAttribute settings = sharedSettings ?? settingsManager.GetSettings(operationRequest, HubconTransportAttribute.GetDefault<WebSocketTransport>(), () => new RateLimitAttribute());

                        if (_ingestRouters.TryGetValue(id, out _))
                            return;

                        var observable = new GenericObservable<JsonElement>(converter);

                        var bufferOptions = new BoundedChannelOptions(settings.QueueLimit)
                        {
                            FullMode = BoundedChannelFullMode.Wait,
                            Capacity = settings.QueueLimit,
                            SingleReader = true,
                            SingleWriter = false,
                            AllowSynchronousContinuations = false,
                        };

                        var observer = AsyncObserver.Create<JsonElement>(converter, bufferOptions);
                        observable.Subscribe(observer);

                        var hw = new HeartbeatWatcher(options.IngestTimeout, async () =>
                        {
                            observable.OnCompleted();
                            _ingestRouters.TryRemove(id, out var complete);
                            complete.Item2?.CancelAsync();
                            complete.Item2?.Dispose();
                            complete.Item4?.RateBucket.Dispose();
                            await rateLimiterManager.Unlink(connectionId, id);
                        });

                        watchers.Add(hw);
                        await rateLimiterManager.Link(connectionId, id, HubconTransportAttribute.GetDefault<WebSocketTransport>(), operationRequest);
                        _ingestRouters.TryAdd(id, (observable, localCts, hw, settings));
                        sources.TryAdd(id, observer.GetAsyncEnumerable());
                    }

                    using var scope = context.RequestServices.CreateAsyncScope();

                    var ingestTask = DefaultEntrypoint.HandleIngest(
                        operationRequest,
                        HubconTransportAttribute.GetDefault<WebSocketTransport>(),
                        scope.ServiceProvider,
                        sources,
                        null,
                        localCts.Token);

                    await sender.SendAsync(new IngestInitAckMessage(ingestInitMessage.Id, connectionId));
                    var result = await ingestTask;

                    if (sender.State != WebSocketState.Open)
                        return;

                    if (result.Failure)
                    {
                        await HandleError(ingestInitMessage.Id, result);
                        return;
                    }

                    await sender.SendAsync(new IngestResultMessage(ingestInitMessage.Id, connectionId, converter.SerializeToElement(result)));
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex.Message);

                    if (sender.State != WebSocketState.Open)
                        return;

                    await sender.SendAsync(new IngestResultMessage(ingestInitMessage.Id, connectionId, converter.SerializeToElement(ex.Message)));
                }
                finally
                {

                    try
                    {
                        foreach (var watcher in watchers)
                        {
                            await watcher.DisposeAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex.Message);
                    }
                    finally
                    {
                        watchers.Clear();
                    }

                    _ingestHandlers.TryRemove(ingestInitMessage.Id, out _);
                    ingestInitMessage.Dispose();
                }
            }

            async Task HandleOperationInvoke(
                HttpContext context,
                string connectionId,
                WebSocketMessageSender sender,
                OperationInvokeMessage operationInvokeMessage,
                ConcurrentDictionary<Guid, CancellationTokenSource> _tasks,
                WebSocket webSocket,
                IGlobalRateLimiterManager rateLimiterManager,
                CancellationToken cancellationToken)
            {
                using var localCts = new CancellationTokenSource();
                using var registration = cancellationToken.Register(localCts.Cancel);
                IOperationRequest? operationRequest = null;

                IHubconResponse? response = null;

                try
                {
                    if (!_tasks.TryAdd(operationInvokeMessage.Id, localCts))
                        return;

                    if (operationInvokeMessage == null) return;

                    if (!await rateLimiterManager.TryAcquireAsync(connectionId, MessageType.operation_invoke, operationInvokeMessage.Id, 1))
                    {
                        await HandleError(operationInvokeMessage.Id, HubconResponse.TooManyRequests());
                        return;
                    }

                    operationRequest = converter.DeserializeData<OperationRequest>(operationInvokeMessage.Payload)!;

                    using var scope = context.RequestServices.CreateAsyncScope();

                    response = await DefaultEntrypoint.HandleMethodWithResult(
                        operationRequest,
                        HubconTransportAttribute.GetDefault<WebSocketTransport>(),
                        scope.ServiceProvider,
                        null,
                        localCts.Token);

                    if (webSocket.State == WebSocketState.Open)
                    {
                        var message = new OperationResponseMessage(
                            operationInvokeMessage.Id,
                            connectionId,
                            converter.SerializeToElement(response)
                        );

                        await sender.SendAsync(message);
                    }
                }
                finally
                {
                    _tasks.TryRemove(operationInvokeMessage.Id, out _);
                    await localCts.CancelAsync();
                    operationInvokeMessage.Dispose();
                }
            }

            async Task HandleOperationCall(
                HttpContext context,
                string connectionId,
                OperationCallMessage operationCallMessage,
                ConcurrentDictionary<Guid, CancellationTokenSource> tasks,
                IGlobalRateLimiterManager rateLimiterManager,
                CancellationToken cancellationToken)
            {
                using var localCts = new CancellationTokenSource();
                using var registration = cancellationToken.Register(localCts.Cancel);

                try
                {
                    if (!tasks.TryAdd(operationCallMessage.Id, localCts))
                        return;

                    if (!await rateLimiterManager.TryAcquireAsync(connectionId, MessageType.operation_call, operationCallMessage.Id, 1))
                    {
                        await HandleError(operationCallMessage.Id, HubconResponse.TooManyRequests());
                        return;
                    }

                    IOperationRequest operationRequest = converter.DeserializeData<OperationRequest>(operationCallMessage.Payload)!;

                    using var scope = context.RequestServices.CreateAsyncScope();

                    var response = await DefaultEntrypoint.HandleMethodVoid(
                        operationRequest,
                        HubconTransportAttribute.GetDefault<WebSocketTransport>(),
                        scope.ServiceProvider,
                        null,
                        localCts.Token);

                    if (response.Failure)
                    {
                        logger?.LogError(response.Message);
                    }
                }
                catch (Exception ex)
                {
                    logger?.LogError("{}", ex.Message);
                }
                finally
                {
                    tasks.TryRemove(operationCallMessage.Id, out _);
                    operationCallMessage.Dispose();
                }
            }

            async Task HandleAck(
                ConcurrentDictionary<Guid, IRetryableMessage> _ackChannels,
                string connectionId,
                AckMessage ackMessage,
                IGlobalRateLimiterManager rateLimiterManager)
            {
                if (_ackChannels.TryGetValue(ackMessage.Id, out IRetryableMessage? value))
                {
                    await value.AckAsync();

                    _ackChannels.TryRemove(ackMessage.Id, out _);

                    if (!await rateLimiterManager.TryAcquireAsync(connectionId, MessageType.ack, ackMessage.Id, 1))
                    {
                        await HandleError(ackMessage.Id, HubconResponse.TooManyRequests());
                        return;
                    }
                }

                ackMessage.Dispose();
            }

            async Task HandleStream(
                HttpContext context,
                string connectionId,
                ConcurrentDictionary<Guid, CancellationTokenSource> _streams,
                ConcurrentDictionary<Guid, IRetryableMessage> _ackChannels,
                WebSocketMessageSender sender,
                StreamInitMessage streamInitMessage,
                WebSocket webSocket,
                IGlobalRateLimiterManager rateLimiterManager,
                CancellationToken cancellationToken)
            {
                using var localCts = new CancellationTokenSource();
                using var registration = cancellationToken.Register(localCts.Cancel);
                IOperationRequest operationRequest = null!;
                try
                {
                    if (streamInitMessage == null || streamInitMessage.Id == Guid.Empty) return;

                    if (!await rateLimiterManager.TryAcquireAsync(connectionId, MessageType.stream_init, streamInitMessage.Id, 1))
                    {
                        await HandleError(streamInitMessage.Id, HubconResponse.TooManyRequests());
                        return;
                    }

                    if (_streams.ContainsKey(streamInitMessage.Id)) return;

                    _streams.TryAdd(streamInitMessage.Id, localCts);

                    operationRequest = converter.DeserializeData<OperationRequest>(streamInitMessage.Payload)!;

                    using var scope = context.RequestServices.CreateAsyncScope();

                    var streamResult = await DefaultEntrypoint.HandleMethodStream(
                        operationRequest,
                        HubconTransportAttribute.GetDefault<WebSocketTransport>(),
                        scope.ServiceProvider,
                        null,
                        localCts.Token);

                    if (streamResult.Failure)
                    {
                        await HandleError(streamInitMessage.Id, HubconResponse.Unauthorized());
                        return;
                    }

                    await rateLimiterManager.Link(connectionId, streamInitMessage.Id, HubconTransportAttribute.GetDefault<WebSocketTransport>(), operationRequest);

                    var stream = streamResult.Data! as IAsyncEnumerable<object?>;

                    await foreach (var item in stream!.WithCancellation(localCts.Token))
                    {
                        await rateLimiterManager.TryAcquireAsync(connectionId, MessageType.stream_init, streamInitMessage.Id, 0);
                        await rateLimiterManager.TryAcquireAsync(connectionId, MessageType.stream_init, streamInitMessage.Id, 1);

                        if (item != null && item.GetType().IsAssignableTo(typeof(IRetryableMessage)))
                        {
                            IRetryableMessage? retryable = item as IRetryableMessage;
                            var ackId = Guid.NewGuid();
                            _ackChannels.TryAdd(ackId, retryable!);

                            while (await retryable!.CanRetry() && !localCts.IsCancellationRequested)
                            {
                                retryable.GetPayload(out object? message);
                                var edwa = new StreamDataWithAckMessage(streamInitMessage.Id, connectionId, converter.SerializeToElement(message), ackId);
                                await sender.SendAsync(edwa);

                                if (!options.MessageRetryIsEnabled)
                                {
                                    break;
                                }
                            }

                            if (_ackChannels.TryRemove(ackId, out IRetryableMessage? channel))
                                await channel.AckAsync();
                        }
                        else
                        {
                            if (!localCts.IsCancellationRequested)
                            {
                                var response = new StreamDataMessage(
                                    streamInitMessage.Id,
                                    connectionId,
                                    converter.SerializeToElement(item)
                                );

                                await sender.SendAsync(response);
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Cancelado normalmente
                }
                catch (Exception)
                {
                    if (webSocket.State == WebSocketState.Open)
                    {
                        await HandleError(streamInitMessage.Id, HubconResponse.InternalError<string>());
                    }
                }
                finally
                {
                    _streams.TryRemove(streamInitMessage.Id, out _);

                    if (webSocket.State == WebSocketState.Open)
                    {
                        await sender.SendAsync(new StreamCompleteMessage(streamInitMessage.Id, connectionId));
                    }

                    streamInitMessage.Dispose();

                    await rateLimiterManager.Unlink(connectionId, streamInitMessage.Id);
                }
            }

            async Task HandleTokenRefresh(
                HttpContext context,
                WebSocketMessageSender sender,
                string connectionId,
                ConcurrentDictionary<Guid, CancellationTokenSource> _tasks,
                TokenUpdateMessage tokenUpdateMessage,
                WebSocket webSocket,
                IGlobalRateLimiterManager rateLimiterManager,
                CancellationToken cancellationToken)
            {
                using var localCts = new CancellationTokenSource();
                using var registration = cancellationToken.Register(localCts.Cancel);

                if (!await rateLimiterManager.TryAcquireAsync(connectionId, MessageType.token_update, tokenUpdateMessage.Id, 1))
                {
                    await HandleError(tokenUpdateMessage.Id, HubconResponse.TooManyRequests());
                    return;
                }

                var user = await IsAuthorized(context);

                try
                {
                    if (!_tasks.TryAdd(tokenUpdateMessage.Id, localCts))
                        return;

                    if (tokenUpdateMessage == null) return;

                    if (user is null)
                    {
                        await sender.SendAsync(new TokenUpdateResponseMessage(tokenUpdateMessage.Id, connectionId, false, "Token refresh failed."));
                        await webSocket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Unauthorized", localCts.Token);
                        logger?.LogInformation("Websocket re-authentication failed.");
                        return;
                    }

                    context.Request.Headers.Authorization = tokenUpdateMessage.Token;
                    context.User = user.Value.ClaimsPrincipal;
                    connectionSupervisor.UpdateExpiration(connectionId, user.Value.ExpirationTime);
                    await sender.SendAsync(new TokenUpdateResponseMessage(tokenUpdateMessage.Id, connectionId, true, "Token refresh OK."));
                }
                catch (OperationCanceledException)
                {
                    await sender.SendAsync(new TokenUpdateResponseMessage(tokenUpdateMessage.Id,connectionId, false, "Operation cancelled."));
                    await webSocket.CloseAsync(WebSocketCloseStatus.InternalServerError, "Operation cancelled.", localCts.Token);
                    logger.LogInformation("Token refresh update: Operation cancelled.");
                }
                catch (Exception ex)
                {
                    await sender.SendAsync(new TokenUpdateResponseMessage(tokenUpdateMessage.Id,connectionId, false, "Internal server error."));
                    await webSocket.CloseAsync(WebSocketCloseStatus.InternalServerError, "Internal server error.", localCts.Token);
                    logger?.LogError(ex.Message);
                }
                finally
                {
                    _tasks.TryRemove(tokenUpdateMessage.Id, out _);
                    tokenUpdateMessage.Dispose();
                }
            }

            async Task HandleError(Guid id, IResponse error)
            {
                if(webSocket.State != WebSocketState.Open)
                    return;

                var localMessage = new ErrorMessage(id, connectionId, default!);

                localMessage.Error = converter.Serialize(new HubconResponse<string>(
                    error.Success,
                    error.Failure, 
                    error.Message, 
                    error.Error,
                    error.StatusCode,
                    null!));

                await sender.SendAsync(localMessage);
            }

            async Task HandlePing(
                WebSocket webSocket,
                string connectionId,
                WebSocketMessageSender sender,
                Guid lastPingId,
                HeartbeatWatcher heartbeatWatcher,
                PingMessage pingMessage,
                IGlobalRateLimiterManager rateLimiterManager)
            {
                if (lastPingId == pingMessage!.Id)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.InvalidPayloadData, "Ping error", default);
                    return;
                }

                if (!await rateLimiterManager.TryAcquireAsync(connectionId, MessageType.ping, pingMessage.Id, 1))
                {
                    await HandleError(pingMessage.Id, HubconResponse.TooManyRequests());
                    return;
                }

                heartbeatWatcher.NotifyHeartbeat();
                await sender.SendAsync(new PongMessage(pingMessage.Id, connectionId));
                pingMessage.Dispose();
            }
        }
    }
}