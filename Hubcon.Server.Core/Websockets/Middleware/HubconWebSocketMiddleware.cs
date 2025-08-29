using Hubcon.Server.Abstractions.CustomAttributes;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Core.Configuration;
using Hubcon.Server.Core.Entrypoint;
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
using Hubcon.Shared.Core.Websockets.Messages.Subscriptions;
using Hubcon.Shared.Core.Websockets.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.Channels;

namespace Hubcon.Server.Core.Websockets.Middleware
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class HubconWebSocketMiddleware
    {
        private readonly RequestDelegate next;
        private readonly IDynamicConverter converter;
        private readonly IOperationRegistry operationRegistry;
        private readonly ILogger<HubconWebSocketMiddleware> logger;
        private readonly IInternalServerOptions options;

        System.Timers.Timer worker;
        int clientCount = 0;

        public HubconWebSocketMiddleware(
            RequestDelegate next,
            IDynamicConverter converter,
            IOperationRegistry operationRegistry,
            ILogger<HubconWebSocketMiddleware> logger,
            IInternalServerOptions options)
        {
            this.next = next;
            this.converter = converter;
            this.operationRegistry = operationRegistry;
            this.logger = logger;
            this.options = options;


            if (options.WebsocketLoggingEnabled)
            {
                worker = new System.Timers.Timer();
                worker.Interval = 1000;
                worker.Elapsed += (sender, eventArgs) =>
                {
                    logger.LogInformation("Connected clients: {0}", clientCount);
                };
                worker.Start();
            }
        }

        public async Task InvokeAsync(HttpContext context, IServiceProvider serviceProvider)
        {
            if (!context.WebSockets.IsWebSocketRequest || !(context.Request.Path == options.WebSocketPathPrefix))
            {
                await next(context);
                return;
            }

            IOperationConfigRegistry operationConfigRegistry = context.RequestServices.GetRequiredService<IOperationConfigRegistry>();
            IRateLimiterManager rateLimiterManager = context.RequestServices.GetRequiredService<IRateLimiterManager>();
            DefaultEntrypoint entrypoint = context.RequestServices.GetRequiredService<DefaultEntrypoint>();

            TimeSpan timeoutSeconds = options.WebSocketTimeout;
            HeartbeatWatcher _heartbeatWatcher = null!;
            CancellationTokenSource cts = new();
            ConcurrentDictionary<Guid, CancellationTokenSource> _subscriptions = null!;
            ConcurrentDictionary<Guid, CancellationTokenSource> _streams = null!;
            ConcurrentDictionary<Guid, (BaseObservable, CancellationTokenSource, HeartbeatWatcher, IngestSettings)> _ingestRouters = null!;
            ConcurrentDictionary<Guid, (CancellationTokenSource, CancellationTokenRegistration)> _ingestHandlers = null!;
            ConcurrentDictionary<Guid, IRetryableMessage> _ackChannels = null!;
            ConcurrentDictionary<Guid, CancellationTokenSource> _tasks = null!;
            WebSocket webSocket = null!;

            webSocket = await context.WebSockets.AcceptWebSocketAsync();

            var settingsManager = new SettingsManager(operationRegistry, operationConfigRegistry);

            try
            {
                var receiver = new WebSocketMessageReceiver(webSocket, options);
                var sender = new WebSocketMessageSender(webSocket, converter);

                // Esperar connection_init
                TrimmedMemoryOwner? firstMessageJson = await receiver.ReceiveAsync();

                if (firstMessageJson == null || firstMessageJson.Memory.IsEmpty)
                {
                    await CloseWebSocketAsync(webSocket, WebSocketCloseStatus.InvalidPayloadData, "No se recibió un mensaje inicial válido.");
                    return;
                }

                var initMessage = new ConnectionInitMessage(firstMessageJson.Memory);

                if (initMessage == null || initMessage.Type != MessageType.connection_init)
                {
                    var message = $"Se esperaba un mensaje {nameof(MessageType.connection_init)}.";

                    await sender.SendAsync(new ErrorMessage(initMessage?.Id ?? Guid.Empty, message, initMessage));

                    await CloseWebSocketAsync(webSocket, WebSocketCloseStatus.PolicyViolation, message);
                    return;
                }

                await sender.SendAsync(new ConnectionAckMessage(Guid.NewGuid()));

                if (options.WebsocketRequiresAuthorization)
                {
                    var accessToken = context.Request.Query["access_token"];

                    if (string.IsNullOrWhiteSpace(accessToken))
                        return;

                    if (options.WebsocketTokenHandler != null)
                    {
                        try
                        {
                            var user = options.WebsocketTokenHandler.Invoke(accessToken!, context.RequestServices)!;

                            if (user is null)
                            {
                                await webSocket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Unauthorized", default);

                                logger?.LogInformation("Websocket authorization failed, user is null.");
                                return;
                            }

                            context.Request.Headers.Authorization = accessToken;
                            context.User = user;
                        }
                        catch (Exception ex)
                        {
                            await webSocket.CloseAsync(WebSocketCloseStatus.InternalServerError, "Internal server error.", default);

                            logger?.LogInformation(ex, "Error while validating websocket token.");
                            return;
                        }
                    }
                    else
                    {
                        logger?.LogInformation("Websocket requires authorization, but token handler is not configured or is invalid.");
                        return;
                    }
                }
                else
                {
                    context.Request.Headers.Authorization = Guid.NewGuid().ToString("N");
                }

                var lastPingId = Guid.Empty;

                _heartbeatWatcher = new HeartbeatWatcher(timeoutSeconds, () =>
                {
                    cts.CancelAsync();
                    return webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Socket timeout", CancellationToken.None);
                });

                _subscriptions = new();
                _streams = new();
                _ingestRouters = new();
                _ingestHandlers = new();
                _ackChannels = new();
                _tasks = new();

                Interlocked.Increment(ref clientCount);

                while (webSocket.State == WebSocketState.Open)
                {
                    TrimmedMemoryOwner? tmo;

                    try
                    {
                        tmo = await receiver.ReceiveAsync();
                    }
                    catch
                    {
                        break;
                    }

                    if (tmo == null || tmo.Memory.IsEmpty)
                        continue;

                    var message = new BaseMessage(tmo.Memory);

                    if (message.Id == Guid.Empty)
                        continue;

                    switch (message.Type)
                    {
                        case MessageType.ping:
                            if (!options.ThrottlingIsDisabled)
                                await rateLimiterManager.TryAcquireAsync(MessageType.ping, message.Id);
                            
                            if (!options.WebsocketRequiresPing)
                            {
                                await HandleNotAllowed(message.Id, "Ping is disabled.", "", sender);
                                break;
                            }

                            _ = HandlePing(webSocket, sender, lastPingId, _heartbeatWatcher, new PingMessage(tmo.Memory, message.Id, message.Type));
                            break;

                        case MessageType.subscription_init:
                            if (!options.ThrottlingIsDisabled)
                                await rateLimiterManager.TryAcquireAsync(MessageType.subscription_init, message.Id);
                            
                            if (!options.WebSocketSubscriptionIsAllowed)
                            {
                                await HandleNotAllowed(message.Id, "Websocket subscriptions are disabled.", "", sender);
                                break;
                            }

                            _ = HandleSubscribe(
                                context,
                                MessageType.subscription_init,
                                _subscriptions,
                                _ackChannels,
                                sender,
                                new SubscriptionInitMessage(tmo.Memory, message.Id, message.Type),
                                rateLimiterManager,
                                entrypoint,
                                cts.Token);

                            break;

                        case MessageType.subscription_complete:
                            if (!options.ThrottlingIsDisabled)
                                await rateLimiterManager.TryAcquireAsync(MessageType.subscription_complete, message.Id);
                            
                            if (!options.WebSocketSubscriptionIsAllowed)
                            {
                                await HandleNotAllowed(message.Id, "Websocket subscriptions are disabled.", "", sender);
                                break;
                            }
                            
                            _ = HandleUnsubscribe(
                                _subscriptions, 
                                context,
                                entrypoint,
                                new SubscriptionCompleteMessage(tmo.Memory, message.Id, message.Type));

                            break;

                        case MessageType.stream_init:
                            if (!options.ThrottlingIsDisabled)
                                await rateLimiterManager.TryAcquireAsync(MessageType.stream_init, message.Id);
                            
                            if (!options.WebSocketSubscriptionIsAllowed)
                            {
                                await HandleNotAllowed(message.Id, "Websocket streaming is disabled.", "", sender);
                                break;
                            }

                            _ = HandleStream(
                                context, 
                                _streams, 
                                _ackChannels, 
                                sender, 
                                new StreamInitMessage(tmo.Memory, message.Id, message.Type), 
                                webSocket, 
                                rateLimiterManager,
                                entrypoint,
                                cts.Token);

                            break;

                        case MessageType.stream_complete:
                            if (!options.ThrottlingIsDisabled)
                                await rateLimiterManager.TryAcquireAsync(MessageType.stream_complete, message.Id);
                            
                            if (!options.WebSocketSubscriptionIsAllowed)
                            {
                                await HandleNotAllowed(message.Id, "Websocket subscriptions are disabled.", "", sender);
                                break;
                            }

                            _ = HandleUnsubscribe(
                                _subscriptions, 
                                context,
                                entrypoint,
                                new SubscriptionCompleteMessage(tmo.Memory, message.Id, message.Type));

                            break;

                        case MessageType.ack:
                            if (!options.ThrottlingIsDisabled)
                                await rateLimiterManager.TryAcquireAsync(MessageType.ack, message.Id);
                            
                            if (!options.MessageRetryIsEnabled)
                            {
                                await HandleNotAllowed(message.Id, "Message ack is disabled.", "", sender);
                                break;
                            }
                            
                            _ = HandleAck(
                                _ackChannels, 
                                new AckMessage(tmo.Memory, message.Id, message.Type));

                            break;

                        case MessageType.operation_invoke:
                            if (!options.ThrottlingIsDisabled)
                                await rateLimiterManager.TryAcquireAsync(MessageType.operation_invoke, message.Id);
                            
                            if (!options.WebSocketMethodsIsAllowed)
                            {
                                await HandleNotAllowed(message.Id, "Websocket methods are disabled.", "", sender);
                                break;
                            }
                            
                            _ = HandleOperationInvoke(
                                context, 
                                sender, 
                                new OperationInvokeMessage(tmo.Memory, message.Id, message.Type), 
                                _tasks, 
                                webSocket,
                                entrypoint,
                                cts.Token);

                            break;

                        case MessageType.operation_call:
                            if (!options.ThrottlingIsDisabled)
                                await rateLimiterManager.TryAcquireAsync(MessageType.operation_call, message.Id);
                            
                            if (!options.WebSocketMethodsIsAllowed)
                            {
                                await HandleNotAllowed(message.Id, "Websocket controller methods are disabled.", "", sender);
                                break;
                            }

                            _ = HandleOperationCall(
                                context, 
                                new OperationCallMessage(tmo.Memory, message.Id, message.Type), 
                                _tasks,
                                entrypoint,
                                cts.Token);

                            break;

                        case MessageType.ingest_init:
                            if (!options.ThrottlingIsDisabled)
                                await rateLimiterManager.TryAcquireAsync(MessageType.ingest_init, message.Id);

                            if (!options.WebSocketIngestIsAllowed)
                            {
                                await HandleNotAllowed(message.Id, "Websocket ingest is disabled.", "", sender);
                                break;
                            }

                            _ = HandleIngestInit(
                                sender, 
                                new IngestInitMessage(tmo.Memory, message.Id, message.Type), 
                                _ingestHandlers, 
                                _ingestRouters, 
                                settingsManager,
                                operationConfigRegistry,
                                rateLimiterManager,
                                entrypoint,
                                cts.Token);

                            break;

                        case MessageType.ingest_data:
                            if (!options.ThrottlingIsDisabled)
                                await rateLimiterManager.TryAcquireAsync(MessageType.ingest_data, message.Id);
                            
                            if (!options.WebSocketIngestIsAllowed)
                            {
                                await HandleNotAllowed(message.Id, "Websocket ingest is disabled.", "", sender);
                                break;
                            }
                            
                            _ = HandleIngestData(_ingestRouters, new IngestDataMessage(tmo.Memory, message.Id, message.Type));

                            break;

                        case MessageType.ingest_data_with_ack:
                            if (!options.ThrottlingIsDisabled)
                                await rateLimiterManager.TryAcquireAsync(MessageType.ingest_data_with_ack, message.Id);
                            
                            if (!options.WebSocketIngestIsAllowed)
                            {
                                await HandleNotAllowed(message.Id, "Websocket ingest is disabled.", "", sender);
                                break;
                            }

                            _ = HandleIngestDataWithAck(_ingestRouters, sender, new IngestDataWithAckMessage(tmo.Memory, message.Id, message.Type));

                            break;

                        case MessageType.ingest_complete:
                            if (!options.ThrottlingIsDisabled)
                                await rateLimiterManager.TryAcquireAsync(MessageType.ingest_complete, message.Id);
                            
                            if (!options.WebSocketIngestIsAllowed)
                            {
                                await HandleNotAllowed(message.Id, "Websocket ingest is disabled.", "", sender);
                                break;
                            }
                            
                            _ = HandleIngestComplete(_ingestRouters, new IngestCompleteMessage(tmo.Memory, message.Id, message.Type));

                            break;
                        case MessageType.cancel:
                            if (!options.ThrottlingIsDisabled)
                                await rateLimiterManager.TryAcquireAsync(MessageType.cancel, message.Id);
                            
                            if (!options.RemoteCancellationIsAllowed)
                            {
                                break;
                            }

                            CancelTask(message.Id, _tasks);

                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.LogInformation(ex.Message);
            }
            finally
            {
                if(_heartbeatWatcher != null)
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
                        catch (Exception ex)
                        {
                            logger?.LogError(ex.Message);
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

                await rateLimiterManager.DisposeAsync();

                webSocket.Dispose();

                Interlocked.Decrement(ref clientCount);
            }
        }

        private async Task CancelTask(Guid id, ConcurrentDictionary<Guid, CancellationTokenSource> tasks)
        {
            if (!tasks.TryRemove(id, out var task)) return;
            await task.CancelAsync();
            task.Dispose();
        }

        private async Task HandleNotAllowed(Guid id, string messageJson, object? payload, WebSocketMessageSender sender)
        {
            await sender.SendAsync(new ErrorMessage(id, messageJson, payload));
        }

        private async Task HandleIngestComplete(ConcurrentDictionary<Guid, (BaseObservable, CancellationTokenSource, HeartbeatWatcher, IngestSettings)> _ingests, IngestCompleteMessage ingestCompleteMessage)
        {
            foreach (var id in ingestCompleteMessage.StreamIds)
            {
                _ingests.TryRemove(id, out var complete);

                complete.Item1?.OnCompleted();
                complete.Item2?.CancelAsync();
                complete.Item4?.RateBucket.Dispose();

                if (complete.Item3 != null)
                    await complete.Item3.DisposeAsync();
            }
        }

        private async Task HandleIngestDataWithAck(
            ConcurrentDictionary<Guid, (BaseObservable, CancellationTokenSource, HeartbeatWatcher, IngestSettings)> _ingests,
            WebSocketMessageSender sender,
            IngestDataWithAckMessage ingestDataWithAckMessage
            )
        {
            if (ingestDataWithAckMessage == null || !_ingests.TryGetValue(ingestDataWithAckMessage.Id, out var ingestWithAck))
                return;

            ingestWithAck.Item3.NotifyHeartbeat();
            ingestWithAck.Item1.OnNextObject(ingestDataWithAckMessage.Data);

            var ingestDataAckMessage = new IngestDataAckMessage(ingestDataWithAckMessage.Id);
            await sender.SendAsync(ingestDataAckMessage);
        }

        private async Task HandleIngestData(ConcurrentDictionary<Guid, (BaseObservable, CancellationTokenSource, HeartbeatWatcher, IngestSettings)> _ingests, IngestDataMessage ingestDataMessage)
        {
            if (ingestDataMessage == null || !_ingests.TryGetValue(ingestDataMessage.Id, out var ingest))
                return;

            ingest.Item3.NotifyHeartbeat();
            ingest.Item1.OnNextElement(ingestDataMessage.Data);
        }

        private async Task HandleIngestInit(
            WebSocketMessageSender sender, 
            IngestInitMessage ingestInitMessage, 
            ConcurrentDictionary<Guid, (CancellationTokenSource, CancellationTokenRegistration)> _ingestHandlers,
            ConcurrentDictionary<Guid, (BaseObservable, CancellationTokenSource, HeartbeatWatcher, IngestSettings)> _ingestRouters,
            ISettingsManager settingsManager,
            IOperationConfigRegistry operationConfigRegistry,
            IRateLimiterManager rateLimiterManager,
            DefaultEntrypoint entrypoint, 
            CancellationToken cancellationToken)
        {
            Dictionary<Guid, object> sources = new();
            using var localCts = new CancellationTokenSource();
            using var registration = cancellationToken.Register(localCts.Cancel);

            List<HeartbeatWatcher> watchers = new();

            try
            {
                var operationRequest = converter.DeserializeData<OperationRequest>(ingestInitMessage!.Payload)!;

                if (!operationRegistry.GetOperationBlueprint(operationRequest, out var blueprint))
                    return;

                IngestSettingsAttribute ingestSettings = settingsManager.GetSettings(operationRequest, () => IngestSettingsAttribute.Default());

                IngestSettings? sharedSettings = null;

                _ingestHandlers.TryAdd(ingestInitMessage.Id, (localCts, registration));

                foreach (var id in ingestInitMessage!.StreamIds)
                {
                    if (sharedSettings != null && ingestSettings.SharedRateLimiter)
                    {
                        sharedSettings = ingestSettings.Factory();
                    }

                    var settings = sharedSettings ?? ingestSettings.Factory();

                    if (_ingestRouters.TryGetValue(id, out _))
                        return;

                    var observable = new GenericObservable<JsonElement>(converter);

                    var bufferOptions = new BoundedChannelOptions(settings.ChannelCapacity)
                    {
                        FullMode = settings.ChannelFullMode,
                        Capacity = settings.ChannelCapacity,
                        SingleReader = true,
                        SingleWriter = false,
                        AllowSynchronousContinuations = false,
                    };

                    var observer = AsyncObserver.Create<JsonElement>(converter, bufferOptions);
                    observable.Subscribe(observer);
                    var handlerCts = new CancellationTokenSource();
                    var handlerRegistration = localCts.Token.Register(handlerCts.Cancel);

                    var hw = new HeartbeatWatcher(options.IngestTimeout, async () =>
                    {
                        observable.OnCompleted();
                        _ingestRouters.TryRemove(id, out var complete);
                        complete.Item2?.CancelAsync();
                        complete.Item2?.Dispose();
                        complete.Item4?.RateBucket.Dispose();
                        handlerRegistration.Dispose();
                        operationConfigRegistry.Unlink(id);
                        await rateLimiterManager.Unlink(id);
                    });

                    watchers.Add(hw);
                    operationConfigRegistry.Link(id, blueprint!);
                    await rateLimiterManager.Link(id, operationRequest);
                    _ingestRouters.TryAdd(id, (observable, handlerCts, hw, settings));
                    sources.TryAdd(id, observer.GetAsyncEnumerable(handlerCts.Token));
                }

                var ingestTask = entrypoint.HandleIngest(operationRequest, sources, localCts.Token);
                await sender.SendAsync(new IngestInitAckMessage(ingestInitMessage.Id));
                await Task.Delay(100);
                var result = await ingestTask;

                if (sender.State != WebSocketState.Open)
                    return;

                await sender.SendAsync(new IngestResultMessage(ingestInitMessage.Id, converter.SerializeToElement(result)));
            }
            catch (Exception ex)
            {
                logger?.LogError(ex.Message);

                if (sender.State != WebSocketState.Open)
                    return;

                await sender.SendAsync(new IngestResultMessage(ingestInitMessage.Id, converter.SerializeToElement(ex.Message)));
            }
            finally
            {
                foreach (var watcher in watchers)
                {
                    try
                    {
                        await watcher.DisposeAsync();
                        watchers.Remove(watcher);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex.Message);
                    }
                }
                _ingestHandlers.TryRemove(ingestInitMessage.Id, out _);
                await localCts.CancelAsync();
            }
        }

        private class State
        {
            public Guid Id = Guid.Empty!;
            public CancellationTokenSource Cts = null!;
            public ConcurrentDictionary<Guid, CancellationTokenSource> Tasks = null!;
        }

        private async Task HandleOperationInvoke(
            HttpContext context,
            WebSocketMessageSender sender,
            OperationInvokeMessage operationInvokeMessage,
            ConcurrentDictionary<Guid, CancellationTokenSource> _tasks,
            WebSocket webSocket,
            DefaultEntrypoint entrypoint,
            CancellationToken cancellationToken)
        {
            using var localCts = new CancellationTokenSource();
            using var registration = cancellationToken.Register(localCts.Cancel);

            IOperationResponse<JsonElement>? result = null;

            try
            {
                if (!_tasks.TryAdd(operationInvokeMessage.Id, localCts))
                    return;

                if (operationInvokeMessage == null) return;

                IOperationRequest operationRequest = converter.DeserializeData<OperationRequest>(operationInvokeMessage.Payload)!;
                result = await entrypoint.HandleMethodWithResult(operationRequest, localCts.Token);
            }
            catch (OperationCanceledException)
            {
                if (localCts.IsCancellationRequested)
                    result = new BaseOperationResponse<JsonElement>(false, default, "Request aborted.");
                
                logger.LogInformation("Task aborted.");
            }
            catch (Exception ex)
            {
                logger?.LogError(ex.Message);
            }
            finally
            {
                _tasks.TryRemove(operationInvokeMessage.Id, out _);
                await localCts.CancelAsync();

                if (webSocket.State == WebSocketState.Open)
                {
                    var response = new OperationResponseMessage(
                        operationInvokeMessage.Id,
                        converter.SerializeToElement(result)
                    );

                    await sender.SendAsync(response);
                }
            }
        }

        private async Task HandleOperationCall(
            HttpContext context, 
            OperationCallMessage operationCallMessage, 
            ConcurrentDictionary<Guid, CancellationTokenSource> tasks,
            DefaultEntrypoint entrypoint,
            CancellationToken cancellationToken)
        {
            using var localCts = new CancellationTokenSource();
            using var registration = cancellationToken.Register(localCts.Cancel);


            try
            {
                if (!tasks.TryAdd(operationCallMessage.Id, localCts))
                    return;

                IOperationRequest operationRequest = converter.DeserializeData<OperationRequest>(operationCallMessage.Payload)!;
                await entrypoint.HandleMethodVoid(operationRequest, localCts.Token);
            }
            catch (Exception ex)
            {
                logger?.LogError("{}", ex.Message);
            }
            finally
            {
                tasks.TryRemove(operationCallMessage.Id, out _);
                await localCts.CancelAsync();
            }
        }

        private async Task HandleUnsubscribe(
            ConcurrentDictionary<Guid, CancellationTokenSource> subscriptions,
            HttpContext context,
            DefaultEntrypoint entrypoint,
            SubscriptionCompleteMessage subscriptionCompletemessage)
        {
            try
            {
                if (subscriptionCompletemessage == null) return;

                if (subscriptions.TryRemove(subscriptionCompletemessage.Id, out var tokenSource))
                {
                    await tokenSource.CancelAsync();
                    tokenSource.Dispose();
                }
            }
            catch (Exception ex)
            {
                logger?.LogError("{Message}", ex.Message);
            }

            return;
        }

        private static async Task HandleAck(
            ConcurrentDictionary<Guid, IRetryableMessage> _ackChannels,
            AckMessage ackMessage)
        {
            if (_ackChannels.TryGetValue(ackMessage.Id, out IRetryableMessage? value))
            {
                await value.AckAsync();
                _ackChannels.TryRemove(ackMessage.Id, out _);
            }
        }

        private async Task HandleSubscribe(
            HttpContext context,
            MessageType type,
            ConcurrentDictionary<Guid, CancellationTokenSource> _subscriptions,
            ConcurrentDictionary<Guid, IRetryableMessage> _ackChannels,
            WebSocketMessageSender sender,
            SubscriptionInitMessage subscribeMessage,
            IRateLimiterManager rateLimiterManager,
            DefaultEntrypoint entrypoint,
            CancellationToken cancellationToken)
        {
            if (subscribeMessage == null || subscribeMessage.Id == Guid.Empty) return;

            if (_subscriptions.ContainsKey(subscribeMessage.Id)) return;

            using var localCts = new CancellationTokenSource();
            using var registration = cancellationToken.Register(localCts.Cancel);

            try
            {
                _subscriptions.TryAdd(subscribeMessage.Id, localCts);

                IOperationRequest operationRequest = converter.DeserializeData<OperationRequest>(subscribeMessage.Payload)!;

                var streamResult = await entrypoint.HandleSubscription(operationRequest, localCts.Token);

                if (streamResult == null) { return; }

                if (!streamResult.Success)
                {
                    await sender.SendAsync(new ErrorMessage(subscribeMessage.Id, string.IsNullOrWhiteSpace(streamResult.Error) ? "Unknown error" : streamResult.Error));
                    return;
                }

                var stream = streamResult.Data!;

                await foreach (var item in stream.WithCancellation(localCts.Token))
                {
                    if (item != null && item.GetType().IsAssignableTo(typeof(IRetryableMessage)))
                    {
                        IRetryableMessage? retryable = item as IRetryableMessage;
                        var ackId = Guid.NewGuid();
                        _ackChannels.TryAdd(ackId, retryable!);

                        while (await retryable!.CanRetry() && !localCts.IsCancellationRequested)
                        {
                            retryable.GetPayload(out object? message);
                            var edwa = new SubscriptionDataWithAckMessage(subscribeMessage.Id, converter.SerializeToElement(message), ackId);
                            await sender.SendAsync(converter.SerializeToElement(edwa));
                        }

                        if (_ackChannels.TryRemove(ackId, out IRetryableMessage? channel))
                            await channel.FailedAckAsync();
                    }
                    else
                    {
                        if (!localCts.IsCancellationRequested)
                        {
                            var response = new SubscriptionDataMessage(
                                subscribeMessage.Id,
                                converter.SerializeToElement(item)
                            );

                            await sender.SendAsync(response);
                        }
                    }

                    if (!options.ThrottlingIsDisabled)
                    {
                        await rateLimiterManager.TryAcquireAsync(type, operationRequest);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Cancelado normalmente
            }
            catch (Exception ex)
            {
                // TODO: Revisar
                await sender.SendAsync(new ErrorMessage(subscribeMessage.Id, ex.Message));
            }
            finally
            {
                _subscriptions.TryRemove(subscribeMessage.Id, out _);
                await localCts.CancelAsync();
            }
        }

        private async Task HandleStream(
            HttpContext context,
            ConcurrentDictionary<Guid, CancellationTokenSource> _streams,
            ConcurrentDictionary<Guid, IRetryableMessage> _ackChannels,
            WebSocketMessageSender sender,
            StreamInitMessage streamInitMessage,
            WebSocket webSocket,
            IRateLimiterManager rateLimiterManager,
            DefaultEntrypoint entrypoint,
            CancellationToken cancellationToken)
        {
            using var localCts = new CancellationTokenSource();
            using var registration = cancellationToken.Register(localCts.Cancel);

            try
            {
                if (streamInitMessage == null || streamInitMessage.Id == Guid.Empty) return;

                if (_streams.ContainsKey(streamInitMessage.Id)) return;

                _streams.TryAdd(streamInitMessage.Id, localCts);

                IOperationRequest operationRequest = converter.DeserializeData<OperationRequest>(streamInitMessage.Payload)!;
                var streamResult = await entrypoint.HandleMethodStream(operationRequest, localCts.Token);

                if (streamResult == null) { return; }

                if (!streamResult.Success)
                {
                    await sender.SendAsync(new ErrorMessage(streamInitMessage.Id, string.IsNullOrWhiteSpace(streamResult.Error) ? "Unknown error" : streamResult.Error));
                    return;
                }

                var stream = streamResult.Data!;

                await foreach (var item in stream.WithCancellation(localCts.Token))
                {
                    await rateLimiterManager.TryAcquireAsync(MessageType.stream_init, streamInitMessage.Id);

                    if (item != null && item.GetType().IsAssignableTo(typeof(IRetryableMessage)))
                    {
                        IRetryableMessage? retryable = item as IRetryableMessage;
                        var ackId = Guid.NewGuid();
                        _ackChannels.TryAdd(ackId, retryable!);

                        while (await retryable!.CanRetry() && !localCts.IsCancellationRequested)
                        {
                            retryable.GetPayload(out object? message);
                            var edwa = new StreamDataWithAckMessage(streamInitMessage.Id, converter.SerializeToElement(message), ackId);
                            await sender.SendAsync(converter.SerializeToElement(edwa));

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
            catch (Exception ex)
            {
                await sender.SendAsync(new ErrorMessage(streamInitMessage.Id, ex.Message));
            }
            finally
            {
                _streams.TryRemove(streamInitMessage.Id, out _);
                await localCts.CancelAsync();

                if(webSocket.State == WebSocketState.Open)
                {
                    await sender.SendAsync(new StreamCompleteMessage(streamInitMessage.Id));
                }
            };
        }

        private static async Task HandlePing(
            WebSocket webSocket,
            WebSocketMessageSender sender,
            Guid lastPingId,
            HeartbeatWatcher heartbeatWatcher,
            PingMessage pingMessage)
        {
            if (lastPingId == pingMessage!.Id)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.InvalidPayloadData, "Ping error", default);
                return;
            }

            heartbeatWatcher.NotifyHeartbeat();

            await sender.SendAsync(new PongMessage(pingMessage.Id));
        }

        private static async Task CloseWebSocketAsync(
            WebSocket webSocket,
            WebSocketCloseStatus status,
            string description)
        {
            if (webSocket.State == WebSocketState.Open || webSocket.State == WebSocketState.CloseReceived)
            {
                await webSocket.CloseAsync(status, description, CancellationToken.None);
            }
        }
    }
}
