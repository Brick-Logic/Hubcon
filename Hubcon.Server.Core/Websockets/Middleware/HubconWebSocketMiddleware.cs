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
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.Channels;

namespace Hubcon.Server.Core.Websockets.Middleware
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class HubconWebSocketMiddleware(
        RequestDelegate next,
        DefaultEntrypoint entrypoint,
        IDynamicConverter converter,
        IOperationConfigRegistry operationConfigRegistry,
        IOperationRegistry operationRegistry,
        ILogger<HubconWebSocketMiddleware> logger,
        IInternalServerOptions options)
    {
        private readonly TimeSpan timeoutSeconds = options.WebSocketTimeout;
        private HeartbeatWatcher _heartbeatWatcher = null!;
        private int PingMessageThrottle = 1;
        private int AckMessageThrottle = 1;

        public async Task InvokeAsync(HttpContext context, IServiceProvider serviceProvider)
        {
            if (!context.WebSockets.IsWebSocketRequest || !(context.Request.Path == options.WebSocketPathPrefix))
            {
                await next(context);
                return;
            }

            using var webSocket = await context.WebSockets.AcceptWebSocketAsync();

            ConcurrentDictionary<Guid, CancellationTokenSource> _subscriptions = new();
            ConcurrentDictionary<Guid, CancellationTokenSource> _streams = new();
            ConcurrentDictionary<Guid, (BaseObservable, CancellationTokenSource, HeartbeatWatcher)> _ingests = new();
            ConcurrentDictionary<Guid, IRetryableMessage> _ackChannels = new();
            ConcurrentDictionary<CancellationTokenSource, Task> _tasks = new();

            try
            {
                var receiver = new WebSocketMessageReceiver(webSocket, options);
                var sender = new WebSocketMessageSender(webSocket, converter);

                // Esperar connection_init
                var firstMessageJson = await receiver.ReceiveAsync();

                var initMessage = converter.DeserializeData<ConnectionInitMessage>(firstMessageJson!);

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
                    return webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Socket timeout", default);
                });

                while (webSocket.State == WebSocketState.Open)
                {
                    string? message;

                    try
                    {
                        if (!options.ThrottlingIsDisabled && options.WebsocketReceiveThrottleDelay > TimeSpan.Zero)
                            await Task.Delay(options.WebsocketReceiveThrottleDelay);

                        message = await receiver.ReceiveAsync();
                    }
                    catch
                    {
                        break;
                    }

                    if (message == null) break;

                    var baseMessage = converter.DeserializeData<BaseMessage>(message);

                    if (baseMessage == null) continue;

                    switch (baseMessage.Type)
                    {
                        case MessageType.ping:
                            if (!options.WebsocketRequiresPing)
                            {
                                await HandleNotAllowed(baseMessage.Id, "Ping is disabled.", baseMessage, sender);
                                break;
                            }

                            if (!options.ThrottlingIsDisabled && PingMessageThrottle > 0)
                                await Task.Delay(TimeSpan.FromMilliseconds(PingMessageThrottle));

                            var ping = HandlePing(webSocket, sender, lastPingId, message);
                            HandleTask(ping, _tasks);
                            break;

                        case MessageType.subscription_init:
                            if (!options.WebSocketSubscriptionIsAllowed)
                            {
                                await HandleNotAllowed(baseMessage.Id, "Websocket subscriptions are disabled.", baseMessage, sender);
                                break;
                            }

                            if (!options.ThrottlingIsDisabled && options.SubscriptionThrottleDelay > TimeSpan.Zero)
                                await Task.Delay(options.SubscriptionThrottleDelay);

                            var subInit = HandleSubscribe(
                                context,
                                _subscriptions,
                                _ackChannels,
                                _tasks,
                                sender,
                                message);

                            HandleTask(subInit, _tasks);
                            break;

                        case MessageType.subscription_complete:
                            if (!options.WebSocketSubscriptionIsAllowed)
                            {
                                await HandleNotAllowed(baseMessage.Id, "Websocket subscriptions are disabled.", baseMessage, sender);
                                break;
                            }

                            if (!options.ThrottlingIsDisabled && options.SubscriptionThrottleDelay > TimeSpan.Zero)
                                await Task.Delay(options.SubscriptionThrottleDelay);

                            var unsub = HandleUnsubscribe(
                                _subscriptions,
                                context,
                                sender,
                                message);

                            HandleTask(unsub, _tasks);
                            break;

                        case MessageType.stream_init:
                            if (!options.WebSocketSubscriptionIsAllowed)
                            {
                                await HandleNotAllowed(baseMessage.Id, "Websocket streaming is disabled.", baseMessage, sender);
                                break;
                            }

                            if (!options.ThrottlingIsDisabled && options.StreamingThrottleDelay > TimeSpan.Zero)
                                await Task.Delay(options.StreamingThrottleDelay);

                            var streamInit = HandleStream(
                                context,
                                _streams,
                                _ackChannels,
                                _tasks,
                                sender,
                                message);

                            HandleTask(streamInit, _tasks);
                            break;

                        case MessageType.stream_complete:
                            if (!options.WebSocketSubscriptionIsAllowed)
                            {
                                await HandleNotAllowed(baseMessage.Id, "Websocket subscriptions are disabled.", baseMessage, sender);
                                break;
                            }

                            if (!options.ThrottlingIsDisabled && options.StreamingThrottleDelay > TimeSpan.Zero)
                                await Task.Delay(options.StreamingThrottleDelay);

                            var streamComplete = HandleUnsubscribe(
                                _subscriptions,
                                context,
                                sender,
                                message);

                            HandleTask(streamComplete, _tasks);
                            break;

                        case MessageType.ack:
                            if (!options.MessageRetryIsEnabled)
                            {
                                await HandleNotAllowed(baseMessage.Id, "Message ack is disabled.", baseMessage, sender);
                                break;
                            }

                            if (!options.ThrottlingIsDisabled && AckMessageThrottle > 0)
                                await Task.Delay(AckMessageThrottle);

                            var ack = HandleAck(_ackChannels, message);
                            HandleTask(ack, _tasks);
                            break;

                        case MessageType.operation_invoke:
                            if (!options.WebSocketMethodsIsAllowed)
                            {
                                await HandleNotAllowed(baseMessage.Id, "Websocket controller methods are disabled.", baseMessage, sender);
                                break;
                            }

                            if (!options.ThrottlingIsDisabled && options.MethodThrottleDelay > TimeSpan.Zero)
                                await Task.Delay(options.MethodThrottleDelay);

                            var operationInvoke = HandleOperationInvoke(context, sender, message);
                            HandleTask(operationInvoke, _tasks);
                            break;

                        case MessageType.operation_call:
                            if (!options.WebSocketMethodsIsAllowed)
                            {
                                await HandleNotAllowed(baseMessage.Id, "Websocket controller methods are disabled.", baseMessage, sender);
                                break;
                            }

                            if (!options.ThrottlingIsDisabled && options.MethodThrottleDelay > TimeSpan.Zero)
                                await Task.Delay(options.MethodThrottleDelay);

                            var operationCall = HandleOperationCall(context, sender, message);
                            HandleTask(operationCall, _tasks);
                            break;

                        case MessageType.ingest_init:

                            if (!options.WebSocketIngestIsAllowed)
                            {
                                await HandleNotAllowed(baseMessage.Id, "Websocket ingest is disabled.", baseMessage, sender);
                                break;
                            }

                            if (!options.ThrottlingIsDisabled && options.IngestThrottleDelay > TimeSpan.Zero)
                                await Task.Delay(options.IngestThrottleDelay);

                            var ingestInit = HandleIngestInit(_ingests, sender, message);
                            HandleTask(ingestInit, _tasks);
                            break;

                        case MessageType.ingest_data:
                            if (!options.WebSocketIngestIsAllowed)
                            {
                                await HandleNotAllowed(baseMessage.Id, "Websocket ingest is disabled.", baseMessage, sender);
                                break;
                            }

                            IngestSettings? ingestSettings = GetSettings<IngestSettingsAttribute>(baseMessage.Id)?.Settings;

                            if (!options.ThrottlingIsDisabled)
                            {
                                var delay = ingestSettings?.ThrottleDelay ?? options.IngestThrottleDelay;
                                if (delay > TimeSpan.Zero)
                                    await Task.Delay(delay);
                            }

                            var ingestData = HandleIngestData(_ingests, message);
                            HandleTask(ingestData, _tasks);
                            break;

                        case MessageType.ingest_data_with_ack:
                            if (!options.WebSocketIngestIsAllowed)
                            {
                                await HandleNotAllowed(baseMessage.Id, "Websocket ingest is disabled.", baseMessage, sender);
                                break;
                            }

                            IngestSettings? ingestWithAckSettings = GetSettings<IngestSettingsAttribute>(baseMessage.Id)?.Settings;

                            if (!options.ThrottlingIsDisabled)
                            {
                                var delay = ingestWithAckSettings?.ThrottleDelay ?? options.IngestThrottleDelay;
                                if (delay > TimeSpan.Zero)
                                    await Task.Delay(delay);
                            }

                            var ingestDataWitAck = HandleIngestDataWithAck(_ingests, sender, message);
                            HandleTask(ingestDataWitAck, _tasks);
                            break;

                        case MessageType.ingest_complete:
                            if (!options.WebSocketIngestIsAllowed)
                            {
                                await HandleNotAllowed(baseMessage.Id, "Websocket ingest is disabled.", baseMessage, sender);
                                break;
                            }

                            if (!options.ThrottlingIsDisabled && options.IngestThrottleDelay > TimeSpan.Zero)
                                await Task.Delay(options.IngestThrottleDelay);

                            var ingestCompleteTask = HandleIngestComplete(_ingests, message);
                            HandleTask(ingestCompleteTask, _tasks);
                            break;
                        default:
                            // Opcional: ignorar o enviar error por tipo desconocido
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
                foreach (var sub in _subscriptions.Values)
                {
                    sub.Cancel();
                }

                foreach (var channel in _ackChannels.Values)
                {
                    try
                    {
                        await channel.FailedAckAsync();
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex.Message);
                    }
                }

                foreach (var task in _ingests.Values)
                {
                    try
                    {
                        await task.Item3.DisposeAsync();
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex.Message);
                    }
                }

                foreach (var task in _tasks)
                {
                    task.Key.Cancel();
                    try
                    {
                        if (!task.Value.IsCompleted && !task.Value.IsFaulted)
                            await task.Value;
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex.Message);
                    }
                }
            }
        }

        private async Task HandleNotAllowed(Guid id, string messageJson, object? payload, WebSocketMessageSender sender)
        {
            await sender.SendAsync(new ErrorMessage(id, messageJson, payload));
        }

        private async Task HandleIngestComplete(ConcurrentDictionary<Guid, (BaseObservable, CancellationTokenSource, HeartbeatWatcher)> _ingests, string message)
        {
            IngestCompleteMessage ingestCompleteMessage = converter.DeserializeData<IngestCompleteMessage>(message)!;

            foreach (var id in ingestCompleteMessage.StreamIds)
            {
                _ingests.TryRemove(id, out var complete);

                if (complete.Item3 != null)
                    await complete.Item3.DisposeAsync();
            }
        }

        private async Task HandleIngestDataWithAck(
            ConcurrentDictionary<Guid, (BaseObservable, CancellationTokenSource, HeartbeatWatcher)> _ingests,
            WebSocketMessageSender sender,
            string message
            )
        {
            IngestDataWithAckMessage ingestDataWithAckMessage = converter.DeserializeData<IngestDataWithAckMessage>(message)!;

            if (ingestDataWithAckMessage == null || !_ingests.TryGetValue(ingestDataWithAckMessage.Id, out var ingestWithAck))
                return;

            ingestWithAck.Item3.NotifyHeartbeat();
            ingestWithAck.Item1.OnNextObject(ingestDataWithAckMessage.Data);

            var ingestDataAckMessage = new IngestDataAckMessage(ingestDataWithAckMessage.Id);
            await sender.SendAsync(ingestDataAckMessage);
        }

        private async Task HandleIngestData(ConcurrentDictionary<Guid, (BaseObservable, CancellationTokenSource, HeartbeatWatcher)> _ingests, string message)
        {
            IngestDataMessage ingestDataMessage = converter.DeserializeData<IngestDataMessage>(message)!;

            if (ingestDataMessage == null || !_ingests.TryGetValue(ingestDataMessage.Id, out var ingest))
                return;

            ingest.Item3.NotifyHeartbeat();
            ingest.Item1.OnNextObject(ingestDataMessage.Data);
        }

        private T? GetSettings<T>(IOperationRequest operationRequest) where T : Attribute
        {
            if (!operationRegistry.GetOperationBlueprint(operationRequest, out var blueprint))
                return null;

            if (blueprint!.ConfigurationAttributes.TryGetValue(typeof(T), out Attribute? value)
                && value is T ingestSettingsAttribute)
            {
                return ingestSettingsAttribute;
            }

            return null;
        }

        private T? GetSettings<T>(Guid linkId) where T : Attribute
        {
            if (operationConfigRegistry.TryGet(linkId, out var blueprint)
                               && blueprint.ConfigurationAttributes.TryGetValue(typeof(T), out Attribute? value)
                                   && (value is T ingestSettings))
            {
                return ingestSettings;
            }

            return null;
        }

        private async Task HandleIngestInit(
            ConcurrentDictionary<Guid, (BaseObservable, CancellationTokenSource, HeartbeatWatcher)> _ingests,
            WebSocketMessageSender sender,
            string message)
        {
            Dictionary<Guid, object> sources = new();
            IngestInitMessage ingestInitMessage = converter.DeserializeData<IngestInitMessage>(message)!;
            var generalCts = new CancellationTokenSource();

            try
            {
                var operationRequest = converter.DeserializeData<OperationRequest>(ingestInitMessage!.Payload)!;

                if (!operationRegistry.GetOperationBlueprint(operationRequest, out var blueprint))
                    return;

                IngestSettings? ingestSettings = GetSettings<IngestSettingsAttribute>(operationRequest)?.Settings;

                foreach (var id in ingestInitMessage!.StreamIds)
                {
                    if (_ingests.TryGetValue(id, out _))
                        return;

                    var observable = new GenericObservable<JsonElement>(converter);

                    var bufferOptions = new BoundedChannelOptions(ingestSettings?.ChannelCapacity ?? IngestSettings.Default.ChannelCapacity)
                    {
                        FullMode = ingestSettings?.ChannelFullMode ?? IngestSettings.Default.ChannelFullMode,
                        Capacity = ingestSettings?.ChannelCapacity ?? IngestSettings.Default.ChannelCapacity,
                        SingleReader = true,
                        SingleWriter = false,
                        AllowSynchronousContinuations = false,
                    };

                    var observer = AsyncObserver.Create<JsonElement>(converter, bufferOptions);
                    observable.Subscribe(observer);
                    var cts = new CancellationTokenSource();

                    var hw = new HeartbeatWatcher(TimeSpan.FromSeconds(60), () =>
                    {
                        observable.OnCompleted();
                        _ingests.TryRemove(id, out var complete);
                        complete.Item2?.Cancel();
                        complete.Item2?.Dispose();
                        operationConfigRegistry.Unlink(id);
                        return cts.CancelAsync();
                    });

                    operationConfigRegistry.Link(id, blueprint!);
                    _ingests.TryAdd(id, (observable, cts, hw));
                    sources.TryAdd(id, observer.GetAsyncEnumerable(cts.Token));
                }

                var ingestTask = entrypoint.HandleIngest(operationRequest, sources, generalCts.Token);
                await Task.Delay(100);
                await sender.SendAsync(new IngestInitAckMessage(ingestInitMessage.Id));
                var result = await ingestTask;
                await sender.SendAsync(new IngestResultMessage(ingestInitMessage.Id, converter.SerializeToElement(result)));
            }
            finally
            {
                generalCts.Cancel();
            }
        }

        public void HandleTask(Task task, ConcurrentDictionary<CancellationTokenSource, Task> tasks)
        {
            var cts = new CancellationTokenSource();
            tasks.TryAdd(cts, task);

            task.ContinueWith(static (t, r) =>
            {
                var state = (State)r!;
                state.Cts.Cancel();
                state.Tasks.TryRemove(state.Cts, out _);
                state.Cts.Dispose();
            }, new State { Cts = cts, Tasks = tasks });
        }

        private class State
        {
            public CancellationTokenSource Cts = null!;
            public ConcurrentDictionary<CancellationTokenSource, Task> Tasks = null!;
        }

        private async Task HandleOperationInvoke(
            HttpContext context,
            WebSocketMessageSender sender,
            string message)
        {
            IOperationResponse<JsonElement>? result = null;
            var generalCts = new CancellationTokenSource();
            OperationInvokeMessage request = converter.DeserializeData<OperationInvokeMessage>(message)!;

            try
            {
                try
                {
                    if (request == null) return;

                    IOperationRequest operationRequest = converter.DeserializeData<OperationRequest>(request.Payload)!;
                    result = await entrypoint.HandleMethodWithResult(operationRequest, generalCts.Token);
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex.Message);

                    if (context.RequestAborted.IsCancellationRequested)
                        result = new BaseOperationResponse<JsonElement>(false, default, "Request aborted.");
                }

                var response = new OperationResponseMessage(
                    request.Id,
                    converter.SerializeToElement(result)
                );

                await sender.SendAsync(response);
            }
            finally
            {
                generalCts.Cancel();
            }
        }

        private async Task HandleOperationCall(
            HttpContext context,
            WebSocketMessageSender sender,
            string message)
        {
            var generalCts = new CancellationTokenSource();
            OperationCallMessage? request = converter.DeserializeData<OperationCallMessage>(message)!;
            try
            {
                if (request == null) return;

                IOperationRequest operationRequest = converter.DeserializeData<OperationRequest>(request.Payload)!;
                await entrypoint.HandleMethodVoid(operationRequest, generalCts.Token);
            }
            catch (Exception ex)
            {
                logger?.LogError($"{ex.Message}");
            }
            finally
            {
                generalCts.Cancel();
            }
        }

        private async Task HandleUnsubscribe(
            ConcurrentDictionary<Guid, CancellationTokenSource> _subscriptions,
            HttpContext context,
            WebSocketMessageSender sender,
            string message)
        {
            SubscriptionCompleteMessage request = converter.DeserializeData<SubscriptionCompleteMessage>(message)!;

            try
            {
                if (request == null) return;

                if (request != null && _subscriptions.TryRemove(request.Id, out var tokenSource))
                    tokenSource.Cancel();
            }
            catch (Exception ex)
            {
                logger?.LogError($"{ex.Message}");
            }
        }

        private async Task HandleAck(
            ConcurrentDictionary<Guid, IRetryableMessage> _ackChannels,
            string message)
        {
            AckMessage ack = converter.DeserializeData<AckMessage>(message)!;

            if (_ackChannels.TryGetValue(ack.Id, out IRetryableMessage? value))
            {
                await value.AckAsync();
                _ackChannels.TryRemove(ack.Id, out _);
            }
        }

        private async Task HandleSubscribe(
            HttpContext context,
            ConcurrentDictionary<Guid, CancellationTokenSource> _subscriptions,
            ConcurrentDictionary<Guid, IRetryableMessage> _ackChannels,
            ConcurrentDictionary<CancellationTokenSource, Task> _tasks,
            WebSocketMessageSender sender,
            string message
            )
        {
            SubscriptionInitMessage subscribe = converter.DeserializeData<SubscriptionInitMessage>(message)!;

            if (subscribe == null || subscribe.Id == Guid.Empty) return;

            if (_subscriptions.ContainsKey(subscribe.Id)) return;

            var cts = new CancellationTokenSource();
            _subscriptions.TryAdd(subscribe.Id, cts);

            var subTaskToken = new CancellationTokenSource();
            var subTask = Task.Run(async () =>
            {
                try
                {
                    IOperationRequest operationRequest = converter.DeserializeData<OperationRequest>(subscribe.Payload)!;

                    var operationConfig = GetSettings<SubscriptionSettingsAttribute>(operationRequest)?.Settings;

                    var streamResult = await entrypoint.HandleSubscription(operationRequest, cts.Token);

                    if (streamResult == null) { return; }

                    if (!streamResult.Success)
                    {
                        await sender.SendAsync(new ErrorMessage(subscribe.Id, string.IsNullOrWhiteSpace(streamResult.Error) ? "Unknown error" : streamResult.Error));
                        return;
                    }

                    var stream = streamResult.Data!;

                    await foreach (var item in stream.WithCancellation(cts.Token))
                    {
                        if (item != null && item.GetType().IsAssignableTo(typeof(IRetryableMessage)))
                        {
                            IRetryableMessage? retryable = item as IRetryableMessage;
                            var ackId = Guid.NewGuid();
                            _ackChannels.TryAdd(ackId, retryable!);

                            while (await retryable!.CanRetry() && !subTaskToken.IsCancellationRequested)
                            {
                                retryable.GetPayload(out object? message);
                                var edwa = new SubscriptionDataWithAckMessage(subscribe.Id, converter.SerializeToElement(message), ackId);
                                await sender.SendAsync(converter.SerializeToElement(edwa));
                            }

                            if (_ackChannels.TryRemove(ackId, out IRetryableMessage? channel))
                                await channel.FailedAckAsync();
                        }
                        else
                        {
                            if (!subTaskToken.IsCancellationRequested)
                            {
                                var response = new SubscriptionDataMessage(
                                    subscribe.Id,
                                    converter.SerializeToElement(item)
                                );

                                await sender.SendAsync(response);
                            }
                        }

                        if (!options.ThrottlingIsDisabled)
                        {
                            var delay = operationConfig?.ThrottleDelay ?? options.MethodThrottleDelay;

                            if (delay > TimeSpan.Zero)
                                await Task.Delay(delay);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Cancelado normalmente
                }
                catch (Exception ex)
                {
                    await sender.SendAsync(new ErrorMessage(subscribe.Id, ex.Message));
                }
                finally
                {
                    cts.Cancel();
                }
            }).ContinueWith(x =>
            {
                _tasks.TryRemove(subTaskToken, out _);

                if (x.IsFaulted)
                {
                    var ex = x.Exception;
                    logger?.LogError(ex.Message);
                }
            });

            _tasks.TryAdd(subTaskToken, subTask);
        }

        private async Task HandleStream(
            HttpContext context,
            ConcurrentDictionary<Guid, CancellationTokenSource> _streams,
            ConcurrentDictionary<Guid, IRetryableMessage> _ackChannels,
            ConcurrentDictionary<CancellationTokenSource, Task> _tasks,
            WebSocketMessageSender sender,
            string message)
        {
            StreamInitMessage? streamInit = converter.DeserializeData<StreamInitMessage>(message)!;

            if (streamInit == null || streamInit.Id == Guid.Empty) return;

            if (_streams.ContainsKey(streamInit.Id)) return;

            var cts = new CancellationTokenSource();
            _streams.TryAdd(streamInit.Id, cts);

            var subTaskToken = new CancellationTokenSource();
            var subTask = Task.Run(async () =>
            {
                try
                {
                    IOperationRequest operationRequest = converter.DeserializeData<OperationRequest>(streamInit.Payload)!;
                    var streamResult = await entrypoint.HandleMethodStream(operationRequest, cts.Token);

                    StreamingSettings? streamingSettings = GetSettings<StreamingSettingsAttribute>(operationRequest)?.Settings;

                    if (streamResult == null) { return; }

                    if (!streamResult.Success)
                    {
                        await sender.SendAsync(new ErrorMessage(streamInit.Id, string.IsNullOrWhiteSpace(streamResult.Error) ? "Unknown error" : streamResult.Error));
                        return;
                    }

                    var stream = streamResult.Data!;

                    await foreach (var item in stream.WithCancellation(cts.Token))
                    {
                        if (item != null && item.GetType().IsAssignableTo(typeof(IRetryableMessage)))
                        {
                            IRetryableMessage? retryable = item as IRetryableMessage;
                            var ackId = Guid.NewGuid();
                            _ackChannels.TryAdd(ackId, retryable!);

                            while (await retryable!.CanRetry() && !subTaskToken.IsCancellationRequested)
                            {
                                retryable.GetPayload(out object? message);
                                var edwa = new StreamDataWithAckMessage(streamInit.Id, converter.SerializeToElement(message), ackId);
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
                            if (!subTaskToken.IsCancellationRequested)
                            {
                                var response = new StreamDataMessage(
                                    streamInit.Id,
                                    converter.SerializeToElement(item)
                                );

                                await sender.SendAsync(response);
                            }
                        }

                        if (!options.ThrottlingIsDisabled)
                        {
                            var delay = streamingSettings?.ThrottleDelay ?? options.IngestThrottleDelay;

                            if (delay > TimeSpan.Zero)
                                await Task.Delay(delay);
                        }
                    }
                }
                catch (OperationCanceledException ex)
                {
                    // Cancelado normalmente
                }
                catch (Exception ex)
                {
                    await sender.SendAsync(new ErrorMessage(streamInit.Id, ex.Message));
                }
                finally
                {
                    cts.Cancel();
                }
            }).ContinueWith(async x =>
            {
                _tasks.TryRemove(subTaskToken, out _);

                if (x.IsFaulted)
                {
                    var ex = x.Exception;
                    logger?.LogError(ex.Message);
                }

                await sender.SendAsync(new StreamCompleteMessage(streamInit.Id));
            });

            _tasks.TryAdd(subTaskToken, subTask);
        }

        private async Task HandlePing(
            WebSocket webSocket,
            WebSocketMessageSender sender,
            Guid lastPingId,
            string message)
        {
            var pingMessage = converter.DeserializeData<PingMessage>(message)!;

            if (lastPingId == pingMessage!.Id)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.InvalidPayloadData, "Ping error", default);
                return;
            }

            _heartbeatWatcher.NotifyHeartbeat();

            await sender.SendAsync(new PongMessage(pingMessage!.Id));
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
