using Hubcon.Server.Abstractions.CustomAttributes;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Core.Configuration;
using Hubcon.Server.Core.Entrypoint;
using Hubcon.Server.Core.RateLimiting;
using Hubcon.Server.Core.Websockets.Helpers;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Core.Tools;
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
        ISettingsManager settingsManager,
        IRateLimiterManager rateLimiterManager,
        ILogger<HubconWebSocketMiddleware> logger,
        IInternalServerOptions options)
    {
        private readonly TimeSpan timeoutSeconds = options.WebSocketTimeout;
        private HeartbeatWatcher _heartbeatWatcher = null!;
        private readonly CancellationTokenSource cts = new();

        public async Task InvokeAsync(HttpContext context, IServiceProvider serviceProvider)
        {
            if (!context.WebSockets.IsWebSocketRequest || !(context.Request.Path == options.WebSocketPathPrefix))
            {
                await next(context);
                return;
            }

            using WebSocket? webSocket = await context.WebSockets.AcceptWebSocketAsync();

            ConcurrentDictionary<Guid, CancellationTokenSource> _subscriptions = null!;
            ConcurrentDictionary<Guid, CancellationTokenSource> _streams = null!;
            ConcurrentDictionary<Guid, (BaseObservable, CancellationTokenSource, HeartbeatWatcher, IngestSettings)> _ingests = null!;
            ConcurrentDictionary<Guid, IRetryableMessage> _ackChannels = null!;
            ConcurrentDictionary<CancellationTokenSource, Task> _tasks = null!;
            settingsManager = new SettingsManager(operationRegistry, operationConfigRegistry);

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
                    cts.Cancel();
                    return webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Socket timeout", default);
                });

                _subscriptions = new();
                _streams = new();
                _ingests = new();
                _ackChannels = new();
                _tasks = new();

                while (webSocket.State == WebSocketState.Open)
                {
                    TrimmedMemoryOwner? message;

                    try
                    {
                        message = await receiver.ReceiveAsync();
                    }
                    catch
                    {
                        break;
                    }

                    if (message == null || message.Memory.IsEmpty)
                        continue;

                    var baseMessage = new BaseMessage(message.Memory);

                    if (baseMessage.Id == Guid.Empty)
                        continue;

                    switch (baseMessage.Type)
                    {
                        case MessageType.ping:
                            if (!options.WebsocketRequiresPing)
                            {
                                await HandleNotAllowed(baseMessage.Id, "Ping is disabled.", "", sender);
                                break;
                            }

                            if (!options.ThrottlingIsDisabled)
                                await rateLimiterManager.TryAcquireAsync(MessageType.ping, baseMessage.Id);

                            var ping = HandlePing(webSocket, sender, lastPingId, new PingMessage(message.Memory));
                            HandleTask(ping, _tasks);
                            break;

                        case MessageType.subscription_init:
                            if (!options.WebSocketSubscriptionIsAllowed)
                            {
                                await HandleNotAllowed(baseMessage.Id, "Websocket subscriptions are disabled.", "", sender);
                                break;
                            }

                            if (!options.ThrottlingIsDisabled)
                                await rateLimiterManager.TryAcquireAsync(MessageType.subscription_init, baseMessage.Id);

                            var subInit = HandleSubscribe(
                                context,
                                MessageType.subscription_init,
                                _subscriptions,
                                _ackChannels,
                                _tasks,
                                sender,
                                new SubscriptionInitMessage(message.Memory));

                            HandleTask(subInit, _tasks);
                            break;

                        case MessageType.subscription_complete:
                            if (!options.WebSocketSubscriptionIsAllowed)
                            {
                                await HandleNotAllowed(baseMessage.Id, "Websocket subscriptions are disabled.", "", sender);
                                break;
                            }

                            if (!options.ThrottlingIsDisabled)
                                await rateLimiterManager.TryAcquireAsync(MessageType.subscription_complete, baseMessage.Id);

                            var unsub = HandleUnsubscribe(
                                _subscriptions,
                                context,
                                sender,
                                new SubscriptionCompleteMessage(message.Memory));

                            HandleTask(unsub, _tasks);
                            break;

                        case MessageType.stream_init:
                            if (!options.WebSocketSubscriptionIsAllowed)
                            {
                                await HandleNotAllowed(baseMessage.Id, "Websocket streaming is disabled.", "", sender);
                                break;
                            }

                            if (!options.ThrottlingIsDisabled)
                                await rateLimiterManager.TryAcquireAsync(MessageType.stream_init, baseMessage.Id);

                            var streamInit = HandleStream(
                                context,
                                _streams,
                                _ackChannels,
                                _tasks,
                                sender,
                                new StreamInitMessage(message.Memory));

                            HandleTask(streamInit, _tasks);
                            break;

                        case MessageType.stream_complete:
                            if (!options.WebSocketSubscriptionIsAllowed)
                            {
                                await HandleNotAllowed(baseMessage.Id, "Websocket subscriptions are disabled.", "", sender);
                                break;
                            }

                            if (!options.ThrottlingIsDisabled)
                                await rateLimiterManager.TryAcquireAsync(MessageType.stream_complete, baseMessage.Id);

                            var streamComplete = HandleUnsubscribe(
                                _subscriptions,
                                context,
                                sender,
                                new SubscriptionCompleteMessage(message.Memory));

                            HandleTask(streamComplete, _tasks);
                            break;

                        case MessageType.ack:
                            if (!options.MessageRetryIsEnabled)
                            {
                                await HandleNotAllowed(baseMessage.Id, "Message ack is disabled.", "", sender);
                                break;
                            }

                            if (!options.ThrottlingIsDisabled)
                                await rateLimiterManager.TryAcquireAsync(MessageType.ack, baseMessage.Id);

                            var ack = HandleAck(_ackChannels, new AckMessage(message.Memory));
                            HandleTask(ack, _tasks);
                            break;

                        case MessageType.operation_invoke:
                            if (!options.WebSocketMethodsIsAllowed)
                            {
                                await HandleNotAllowed(baseMessage.Id, "Websocket methods are disabled.", "", sender);
                                break;
                            }

                            if (!options.ThrottlingIsDisabled)
                                await rateLimiterManager.TryAcquireAsync(MessageType.operation_invoke, baseMessage.Id);

                            var operationInvoke = HandleOperationInvoke(context, sender, new OperationInvokeMessage(message.Memory));
                            HandleTask(operationInvoke, _tasks);
                            break;

                        case MessageType.operation_call:
                            if (!options.WebSocketMethodsIsAllowed)
                            {
                                await HandleNotAllowed(baseMessage.Id, "Websocket controller methods are disabled.", "", sender);
                                break;
                            }

                            if (!options.ThrottlingIsDisabled)
                                await rateLimiterManager.TryAcquireAsync(MessageType.operation_call, baseMessage.Id);

                            var operationCall = HandleOperationCall(context, sender, new OperationCallMessage(message.Memory));
                            HandleTask(operationCall, _tasks);
                            break;

                        case MessageType.ingest_init:

                            if (!options.WebSocketIngestIsAllowed)
                            {
                                await HandleNotAllowed(baseMessage.Id, "Websocket ingest is disabled.", "", sender);
                                break;
                            }

                            if (!options.ThrottlingIsDisabled)
                                await rateLimiterManager.TryAcquireAsync(MessageType.ingest_init, baseMessage.Id);

                            var ingestInit = HandleIngestInit(_ingests, sender, new IngestInitMessage(message.Memory));
                            HandleTask(ingestInit, _tasks);
                            break;

                        case MessageType.ingest_data:
                            if (!options.WebSocketIngestIsAllowed)
                            {
                                await HandleNotAllowed(baseMessage.Id, "Websocket ingest is disabled.", "", sender);
                                break;
                            }

                            if (!options.ThrottlingIsDisabled)
                                await rateLimiterManager.TryAcquireAsync(MessageType.ingest_data, baseMessage.Id);

                            var ingestData = HandleIngestData(_ingests, new IngestDataMessage(message.Memory));
                            HandleTask(ingestData, _tasks);
                            break;

                        case MessageType.ingest_data_with_ack:
                            if (!options.WebSocketIngestIsAllowed)
                            {
                                await HandleNotAllowed(baseMessage.Id, "Websocket ingest is disabled.", "", sender);
                                break;
                            }

                            if (!options.ThrottlingIsDisabled)
                                await rateLimiterManager.TryAcquireAsync(MessageType.ingest_data_with_ack, baseMessage.Id);

                            var ingestDataWitAck = HandleIngestDataWithAck(_ingests, sender, new IngestDataWithAckMessage(message.Memory));
                            HandleTask(ingestDataWitAck, _tasks);
                            break;

                        case MessageType.ingest_complete:
                            if (!options.WebSocketIngestIsAllowed)
                            {
                                await HandleNotAllowed(baseMessage.Id, "Websocket ingest is disabled.", "", sender);
                                break;
                            }

                            if (!options.ThrottlingIsDisabled)
                                await rateLimiterManager.TryAcquireAsync(MessageType.ingest_complete, baseMessage.Id);

                            var ingestCompleteTask = HandleIngestComplete(_ingests, new IngestCompleteMessage(message.Memory));
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
                if (_subscriptions != null)
                {
                    foreach (var sub in _subscriptions.Values)
                    {
                        sub.Cancel();
                    }
                }

                if (_ackChannels != null)
                {
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
                }

                if (_ingests != null)
                {
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
                }

                if (_tasks != null)
                {
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
                complete.Item2?.Cancel();
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
            ingest.Item1.OnNextObject(ingestDataMessage.Data);
        }

        private async Task HandleIngestInit(
            ConcurrentDictionary<Guid, (BaseObservable, CancellationTokenSource, HeartbeatWatcher, IngestSettings)> _ingests,
            WebSocketMessageSender sender,
            IngestInitMessage ingestInitMessage)
        {
            Dictionary<Guid, object> sources = new();
            CancellationTokenSource generalCts = new CancellationTokenSource();
            List<HeartbeatWatcher> watchers = new();

            try
            {
                var operationRequest = converter.DeserializeData<OperationRequest>(ingestInitMessage!.Payload)!;

                if (!operationRegistry.GetOperationBlueprint(operationRequest, out var blueprint))
                    return;

                IngestSettingsAttribute ingestSettings = settingsManager.GetSettings(operationRequest, () => IngestSettingsAttribute.Default());

                IngestSettings? sharedSettings = null;

                foreach (var id in ingestInitMessage!.StreamIds)
                {
                    if (sharedSettings != null && ingestSettings.SharedRateLimiter)
                    {
                        sharedSettings = ingestSettings.Factory();
                    }

                    var settings = sharedSettings ?? ingestSettings.Factory();

                    if (_ingests.TryGetValue(id, out _))
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
                    var cts = new CancellationTokenSource();

                    var hw = new HeartbeatWatcher(options.IngestTimeout, async () =>
                    {
                        observable.OnCompleted();
                        _ingests.TryRemove(id, out var complete);
                        complete.Item2?.Cancel();
                        complete.Item2?.Dispose();
                        complete.Item4?.RateBucket.Dispose();
                        operationConfigRegistry.Unlink(id);
                        await rateLimiterManager.Unlink(id);
                    });

                    watchers.Add(hw);
                    operationConfigRegistry.Link(id, blueprint!);
                    await rateLimiterManager.Link(id, operationRequest);
                    _ingests.TryAdd(id, (observable, cts, hw, settings));
                    sources.TryAdd(id, observer.GetAsyncEnumerable(cts.Token));
                }

                var ingestTask = entrypoint.HandleIngest(operationRequest, sources, generalCts.Token);
                await Task.Delay(100);
                await sender.SendAsync(new IngestInitAckMessage(ingestInitMessage.Id));
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
                generalCts.Cancel();

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
            }
        }

        public void HandleTask(Task task, ConcurrentDictionary<CancellationTokenSource, Task> tasks, Action? cleaningCallback = null)
        {
            var cts = new CancellationTokenSource();
            tasks.TryAdd(cts, task);

            task.ContinueWith((t, r) =>
            {
                cleaningCallback?.Invoke();
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
            OperationInvokeMessage operationInvokeMessage)
        {
            IOperationResponse<JsonElement>? result = null;
            var generalCts = new CancellationTokenSource();

            try
            {
                try
                {
                    if (operationInvokeMessage == null) return;

                    IOperationRequest operationRequest = converter.DeserializeData<OperationRequest>(operationInvokeMessage.Payload)!;
                    result = await entrypoint.HandleMethodWithResult(operationRequest, generalCts.Token);
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex.Message);

                    if (context.RequestAborted.IsCancellationRequested)
                        result = new BaseOperationResponse<JsonElement>(false, default, "Request aborted.");
                }

                var response = new OperationResponseMessage(
                    operationInvokeMessage.Id,
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
            OperationCallMessage operationCallMessage)
        {
            var generalCts = new CancellationTokenSource();
            try
            {
                IOperationRequest operationRequest = converter.DeserializeData<OperationRequest>(operationCallMessage.Payload)!;
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
            SubscriptionCompleteMessage subscriptionCompletemessage)
        {
            try
            {
                if (subscriptionCompletemessage == null) return;

                if (subscriptionCompletemessage != null && _subscriptions.TryRemove(subscriptionCompletemessage.Id, out var tokenSource))
                    tokenSource.Cancel();
            }
            catch (Exception ex)
            {
                logger?.LogError($"{ex.Message}");
            }
        }

        private async Task HandleAck(
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
            ConcurrentDictionary<CancellationTokenSource, Task> _tasks,
            WebSocketMessageSender sender,
            SubscriptionInitMessage subscribeMessage
            )
        {
            if (subscribeMessage == null || subscribeMessage.Id == Guid.Empty) return;

            if (_subscriptions.ContainsKey(subscribeMessage.Id)) return;

            var cts = new CancellationTokenSource();
            _subscriptions.TryAdd(subscribeMessage.Id, cts);

            var subTaskToken = new CancellationTokenSource();
            var subTask = Task.Run(async () =>
            {
                try
                {
                    IOperationRequest operationRequest = converter.DeserializeData<OperationRequest>(subscribeMessage.Payload)!;

                    var streamResult = await entrypoint.HandleSubscription(operationRequest, cts.Token);

                    if (streamResult == null) { return; }

                    if (!streamResult.Success)
                    {
                        await sender.SendAsync(new ErrorMessage(subscribeMessage.Id, string.IsNullOrWhiteSpace(streamResult.Error) ? "Unknown error" : streamResult.Error));
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
                                var edwa = new SubscriptionDataWithAckMessage(subscribeMessage.Id, converter.SerializeToElement(message), ackId);
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
                    await sender.SendAsync(new ErrorMessage(subscribeMessage.Id, ex.Message));
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
            StreamInitMessage streamInitMessage)
        {
            if (streamInitMessage == null || streamInitMessage.Id == Guid.Empty) return;

            if (_streams.ContainsKey(streamInitMessage.Id)) return;

            var cts = new CancellationTokenSource();
            _streams.TryAdd(streamInitMessage.Id, cts);

            var subTaskToken = new CancellationTokenSource();
            var subTask = Task.Run(async () =>
            {
                try
                {
                    IOperationRequest operationRequest = converter.DeserializeData<OperationRequest>(streamInitMessage.Payload)!;
                    var streamResult = await entrypoint.HandleMethodStream(operationRequest, cts.Token);

                    if (streamResult == null) { return; }

                    if (!streamResult.Success)
                    {
                        await sender.SendAsync(new ErrorMessage(streamInitMessage.Id, string.IsNullOrWhiteSpace(streamResult.Error) ? "Unknown error" : streamResult.Error));
                        return;
                    }

                    var stream = streamResult.Data!;

                    await foreach (var item in stream.WithCancellation(cts.Token))
                    {
                        await rateLimiterManager.TryAcquireAsync(MessageType.stream_init, streamInitMessage.Id);

                        if (item != null && item.GetType().IsAssignableTo(typeof(IRetryableMessage)))
                        {
                            IRetryableMessage? retryable = item as IRetryableMessage;
                            var ackId = Guid.NewGuid();
                            _ackChannels.TryAdd(ackId, retryable!);

                            while (await retryable!.CanRetry() && !subTaskToken.IsCancellationRequested)
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
                            if (!subTaskToken.IsCancellationRequested)
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
                catch (OperationCanceledException ex)
                {
                    // Cancelado normalmente
                }
                catch (Exception ex)
                {
                    await sender.SendAsync(new ErrorMessage(streamInitMessage.Id, ex.Message));
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

                await sender.SendAsync(new StreamCompleteMessage(streamInitMessage.Id));
            });

            _tasks.TryAdd(subTaskToken, subTask);
        }

        private async Task HandlePing(
            WebSocket webSocket,
            WebSocketMessageSender sender,
            Guid lastPingId,
            PingMessage pingMessage)
        {
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
