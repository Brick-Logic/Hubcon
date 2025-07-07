using Hubcon.Server.Abstractions.CustomAttributes;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Core.Configuration;
using Hubcon.Server.Core.Entrypoint;
using Hubcon.Server.Core.Routing.Registries;
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
using System.Linq;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;

namespace Hubcon.Server.Core.Websockets.Middleware
{
    public class HubconWebSocketMiddleware(
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

        public static async IAsyncEnumerable<object> GetSubscriptionSource([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            int i = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                yield return $"mensaje {i}";
                i++;
                await Task.Delay(2000, cancellationToken);
            }
        }

        public async Task InvokeAsync(HttpContext context, IServiceProvider serviceProvider)
        {
            if (!context.WebSockets.IsWebSocketRequest || !(context.Request.Path == options.WebSocketPathPrefix))
            {
                await next(context);
                return;
            }

            if (!context.Request.Headers.ContainsKey("Authorization"))
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken))
                {
                    context.Request.Headers["Authorization"] = $"Bearer {accessToken}";
                }
            }

            using var webSocket = await context.WebSockets.AcceptWebSocketAsync();

            ConcurrentDictionary<string, CancellationTokenSource> _subscriptions = new();
            ConcurrentDictionary<string, CancellationTokenSource> _streams = new();
            ConcurrentDictionary<string, (BaseObservable, CancellationTokenSource, HeartbeatWatcher)> _ingests = new();
            ConcurrentDictionary<string, IRetryableMessage> _ackChannels = new();
            ConcurrentDictionary<CancellationTokenSource, Task> _tasks = new();

            try
            {
                var receiver = new WebSocketMessageReceiver(webSocket, options);
                var sender = new WebSocketMessageSender(webSocket);

                // Esperar connection_init
                var firstMessageJson = await receiver.ReceiveAsync();

                var initMessage = converter.DeserializeData<ConnectionInitMessage>(firstMessageJson!);

                if (initMessage == null || initMessage.Type != MessageType.connection_init)
                {
                    var message = $"Se esperaba un mensaje {nameof(MessageType.connection_init)}.";

                    await sender.SendAsync(new ErrorMessage(initMessage?.Id ?? "", message, initMessage));

                    await CloseWebSocketAsync(webSocket, WebSocketCloseStatus.PolicyViolation, message);
                    return;
                }

                await sender.SendAsync(new ConnectionAckMessage());

                var lastPingId = string.Empty;

                _heartbeatWatcher = new HeartbeatWatcher(timeoutSeconds, () =>
                {
                    return webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Socket timeout", default);
                });

                while (webSocket.State == WebSocketState.Open)
                {
                    string? messageJson = null;

                    try
                    {
                        messageJson = await receiver.ReceiveAsync();
                    }
                    catch
                    {
                        break;
                    }

                    if (messageJson == null) break;

                    var baseMessage = converter.DeserializeData<BaseMessage>(messageJson);

                    if (baseMessage == null) continue;

                    switch (baseMessage.Type)
                    {
                        case MessageType.ping:
                            if (!options.WebsocketRequiresPing)
                            {
                                await HandleNotAllowed(baseMessage.Id, "Ping is disabled.", baseMessage, sender);
                                break;
                            }

                            var ping = HandlePing(webSocket, sender, lastPingId, messageJson);
                            HandleTask(ping, _tasks);
                            break;

                        case MessageType.subscription_init:
                            if (!options.WebSocketSubscriptionIsAllowed)
                            {
                                await HandleNotAllowed(baseMessage.Id, "Websocket subscriptions are disabled.", baseMessage, sender);
                                break;
                            }

                            var subInit = HandleSubscribe(context, _subscriptions, _ackChannels, _tasks, sender, messageJson);
                            HandleTask(subInit, _tasks);
                            break;

                        case MessageType.subscription_complete:
                            if (!options.WebSocketSubscriptionIsAllowed)
                            {
                                await HandleNotAllowed(baseMessage.Id, "Websocket subscriptions are disabled.", baseMessage, sender);
                                break;
                            }

                            var unsub = HandleUnsubscribe(_subscriptions, context, sender, messageJson);
                            HandleTask(unsub, _tasks);
                            break;

                        case MessageType.stream_init:
                            if (!options.WebSocketSubscriptionIsAllowed)
                            {
                                await HandleNotAllowed(baseMessage.Id, "Websocket streaming is disabled.", baseMessage, sender);
                                break;
                            }

                            var streamInit = HandleStream(context, _streams, _ackChannels, _tasks, sender, messageJson);
                            HandleTask(streamInit, _tasks);
                            break;

                        case MessageType.stream_complete:
                            if (!options.WebSocketSubscriptionIsAllowed)
                            {
                                await HandleNotAllowed(baseMessage.Id, "Websocket subscriptions are disabled.", baseMessage, sender);
                                break;
                            }

                            var streamComplete = HandleUnsubscribe(_subscriptions, context, sender, messageJson);
                            HandleTask(streamComplete, _tasks);
                            break;

                        case MessageType.ack:
                            if (!options.MessageRetryIsEnabled)
                            {
                                await HandleNotAllowed(baseMessage.Id, "Message ack is disabled.", baseMessage, sender);
                                break;
                            }

                            var ack = HandleAck(_ackChannels, messageJson);
                            HandleTask(ack, _tasks);
                            break;

                        case MessageType.operation_invoke:
                            if (!options.WebSocketMethodsIsAllowed)
                            {
                                await HandleNotAllowed(baseMessage.Id, "Websocket controller methods are disabled.", baseMessage, sender);
                                break;
                            }

                            var operationInvoke = HandleOperationInvoke(context, sender, messageJson);
                            HandleTask(operationInvoke, _tasks);
                            break;

                        case MessageType.operation_call:
                            if (!options.WebSocketMethodsIsAllowed)
                            {
                                await HandleNotAllowed(baseMessage.Id, "Websocket controller methods are disabled.", baseMessage, sender);
                                break;
                            }

                            var operationCall = HandleOperationCall(context, sender, messageJson);
                            HandleTask(operationCall, _tasks);
                            break;

                        case MessageType.ingest_init:
                            if (!options.WebSocketIngestIsAllowed)
                            {
                                await HandleNotAllowed(baseMessage.Id, "Websocket ingest is disabled.", baseMessage, sender);
                                break;
                            }

                            var ingestInit = HandleIngestInit(_ingests, sender, messageJson);
                            HandleTask(ingestInit, _tasks);
                            break;

                        case MessageType.ingest_data:
                            if (!options.WebSocketIngestIsAllowed)
                            {
                                await HandleNotAllowed(baseMessage.Id, "Websocket ingest is disabled.", baseMessage, sender);
                                break;
                            }

                            IngestSettings? ingestSettings = GetSettings<IngestSettingsAttribute>(baseMessage.Id)?.Settings;
                            
                            if (ingestSettings != null && ingestSettings.ThrottleDelay >= TimeSpan.Zero)
                                await Task.Delay(ingestSettings.ThrottleDelay);                           

                            var ingestData = HandleIngestData(_ingests, messageJson);
                            HandleTask(ingestData, _tasks);
                            break;

                        case MessageType.ingest_data_with_ack:
                            if (!options.WebSocketIngestIsAllowed)
                            {
                                await HandleNotAllowed(baseMessage.Id, "Websocket ingest is disabled.", baseMessage, sender);
                                break;
                            }

                            var ingestDataWitAck = HandleIngestDataWithAck(_ingests, sender, messageJson);
                            HandleTask(ingestDataWitAck, _tasks);
                            break;

                        case MessageType.ingest_complete:
                            if (!options.WebSocketIngestIsAllowed)
                            {
                                await HandleNotAllowed(baseMessage.Id, "Websocket ingest is disabled.", baseMessage, sender);
                                break;
                            }

                            await HandleIngestComplete(_ingests, messageJson);
                            break;
                        default:
                            // Opcional: ignorar o enviar error por tipo desconocido
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex.Message);
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
                        logger.LogError(ex.Message);
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
                        logger.LogError(ex.Message);
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
                        logger.LogError(ex.Message);
                    }
                }
            }
        }

        private async Task HandleNotAllowed(string id, string messageJson, object? payload, WebSocketMessageSender sender)
        {
            await sender.SendAsync(new ErrorMessage(id, messageJson, payload));
        }

        private async Task HandleIngestComplete(ConcurrentDictionary<string, (BaseObservable, CancellationTokenSource, HeartbeatWatcher)> _ingests, string messageJson)
        {
            var ingestCompleteMessage = converter.DeserializeData<IngestCompleteMessage>(messageJson);

            foreach (var id in ingestCompleteMessage.StreamIds)
            {
                _ingests.TryRemove(id, out var complete);
                await complete.Item3.DisposeAsync();
            }
        }

        private async Task HandleIngestDataWithAck(ConcurrentDictionary<string, (BaseObservable, CancellationTokenSource, HeartbeatWatcher)> _ingests, WebSocketMessageSender sender, string messageJson)
        {
            var ingestDataWithAckMessage = converter.DeserializeData<IngestDataWithAckMessage>(messageJson);

            if (ingestDataWithAckMessage == null || !_ingests.TryGetValue(ingestDataWithAckMessage.Id, out var ingestWithAck))
                return;

            ingestWithAck.Item3.NotifyHeartbeat();
            ingestWithAck.Item1.OnNextObject(ingestDataWithAckMessage.Data);

            var ingestDataAckMessage = new IngestDataAckMessage(ingestDataWithAckMessage.Id);
            await sender.SendAsync(ingestDataAckMessage);
        }

        private async Task HandleIngestData(ConcurrentDictionary<string, (BaseObservable, CancellationTokenSource, HeartbeatWatcher)> _ingests, string messageJson)
        {
            var ingestDataMessage = converter.DeserializeData<IngestDataMessage>(messageJson);

            if (ingestDataMessage == null || !_ingests.TryGetValue(ingestDataMessage.Id, out var ingest))
                return;

            ingest.Item3.NotifyHeartbeat();
            ingest.Item1.OnNextElement(ingestDataMessage.Data);
        }

        private T? GetSettings<T>(IOperationRequest operationRequest) where T: Attribute
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

        private T? GetSettings<T>(string linkId) where T : Attribute
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
            ConcurrentDictionary<string, (BaseObservable, CancellationTokenSource, HeartbeatWatcher)> _ingests,
            WebSocketMessageSender sender,
            string messageJson)
        {
            var ingestInitMessage = converter.DeserializeData<IngestInitMessage>(messageJson);

            Dictionary<string, object> sources = new();

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

                var observer = new AsyncObserver<JsonElement>(bufferOptions);
                observable.Subscribe(observer);
                var cts = new CancellationTokenSource();

                var hw = new HeartbeatWatcher(TimeSpan.FromSeconds(60), () =>
                {
                    observable.OnCompleted();
                    _ingests.TryRemove(id, out var complete);
                    complete.Item2?.Cancel();
                    complete.Item2?.Dispose();
                    return cts.CancelAsync();
                });

                _ingests.TryAdd(id, (observable, cts, hw));
                sources.TryAdd(id, observer.GetAsyncEnumerable(cts.Token));
            }

            var ingestTask = entrypoint.HandleIngest(operationRequest, sources);
            await Task.Delay(100);
            await sender.SendAsync(new IngestInitAckMessage(ingestInitMessage.Id));
            await ingestTask;
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
            string messageJson)
        {
            OperationInvokeMessage request = null!;
            object? result = false;

            try
            {
                request = converter.DeserializeData<OperationInvokeMessage>(messageJson)!;

                if (request == null) return;

                try
                {
                    IOperationRequest operationRequest = converter.DeserializeData<OperationRequest>(request.Payload)!;
                    result = await entrypoint.HandleMethodWithResult(operationRequest);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex.Message);

                    if (context.RequestAborted.IsCancellationRequested)
                        result = null;
                }

            }
            catch (Exception ex)
            {
                result = null;
                logger.LogError($"{ex.Message}");
            }

            var response = new OperationResponseMessage(
                request.Id,
                converter.SerializeToElement(result)
            );

            await sender.SendAsync(response);
        }

        private async Task HandleOperationCall(
            HttpContext context,
            WebSocketMessageSender sender,
            string messageJson)
        {
            OperationInvokeMessage request = null!;
            object? result = false;

            try
            {
                request = converter.DeserializeData<OperationInvokeMessage>(messageJson)!;

                if (request == null) return;

                IOperationRequest operationRequest = converter.DeserializeData<OperationRequest>(request.Payload)!;
                result = entrypoint.HandleMethodWithResult(operationRequest);

                if (context.RequestAborted.IsCancellationRequested)
                    result = false;

                var response = new OperationResponseMessage(
                    request.Id,
                    converter.SerializeToElement(result)
                );

                await sender.SendAsync(response);
            }
            catch (Exception ex)
            {
                result = false;
                logger.LogError($"{ex.Message}");
            }
        }

        private async Task HandleUnsubscribe(
            ConcurrentDictionary<string, CancellationTokenSource> _subscriptions,
            HttpContext context,
            WebSocketMessageSender sender,
            string messageJson)
        {
            SubscriptionCompleteMessage request = null!;
            object? result = false;

            try
            {
                request = converter.DeserializeData<SubscriptionCompleteMessage>(messageJson)!;

                if (request == null) return;

                if (request != null && _subscriptions.TryRemove(request.Id, out var tokenSource))
                    tokenSource.Cancel();
            }
            catch (Exception ex)
            {
                result = false;
                logger.LogError($"{ex.Message}");
            }
        }

        private async Task HandleStreamComplete(
            ConcurrentDictionary<string, CancellationTokenSource> _streams,
            HttpContext context,
            WebSocketMessageSender sender,
            string messageJson)
        {
            SubscriptionCompleteMessage request = null!;
            object? result = false;

            try
            {
                request = converter.DeserializeData<SubscriptionCompleteMessage>(messageJson)!;

                if (request == null) return;

                if (request != null && _streams.TryRemove(request.Id, out var tokenSource))
                    tokenSource.Cancel();
            }
            catch (Exception ex)
            {
                result = false;
                logger.LogError($"{ex.Message}");
            }
        }

        private async Task HandleAck(
            ConcurrentDictionary<string,
                IRetryableMessage> _ackChannels,
            string messageJson)
        {
            var ack = converter.DeserializeData<AckMessage>(messageJson)!;

            if (_ackChannels.TryGetValue(ack.Id.ToString(), out IRetryableMessage? value))
            {
                await value.AckAsync();
                _ackChannels.TryRemove(ack.Id.ToString(), out _);
            }
        }

        private async Task HandleSubscribe(
            HttpContext context,
            ConcurrentDictionary<string, CancellationTokenSource> _subscriptions,
            ConcurrentDictionary<string, IRetryableMessage> _ackChannels,
            ConcurrentDictionary<CancellationTokenSource, Task> _tasks,
            WebSocketMessageSender sender,
            string messageJson)
        {
            var subscribe = converter.DeserializeData<SubscriptionInitMessage>(messageJson);

            if (subscribe == null || string.IsNullOrWhiteSpace(subscribe.Id)) return;

            if (_subscriptions.ContainsKey(subscribe.Id)) return;

            var cts = new CancellationTokenSource();
            _subscriptions.TryAdd(subscribe.Id, cts);

            var subTaskToken = new CancellationTokenSource();
            var subTask = Task.Run(async () =>
            {
                try
                {
                    IOperationRequest operationRequest = converter.DeserializeData<OperationRequest>(subscribe.Payload)!;
                    var stream = await entrypoint.HandleSubscription(operationRequest);

                    if (stream == null) { return; }

                    await foreach (var item in stream.WithCancellation(cts.Token))
                    {
                        if (item != null && item.GetType().IsAssignableTo(typeof(IRetryableMessage)))
                        {
                            IRetryableMessage? retryable = item as IRetryableMessage;
                            var ackId = Guid.NewGuid().ToString();
                            _ackChannels.TryAdd(ackId, retryable!);

                            while (await retryable!.CanRetry() && !subTaskToken.IsCancellationRequested)
                            {
                                retryable.GetPayload(out object? message);
                                var edwa = new SubscriptionDataWithAckMessage(subscribe.Id, converter.SerializeToElement(message), ackId);
                                await sender.SendAsync(converter.SerializeToElement(edwa));
                            }

                            if (_ackChannels.TryRemove(ackId.ToString(), out IRetryableMessage? channel))
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
            }).ContinueWith(x =>
            {
                _tasks.TryRemove(subTaskToken, out _);

                if (x.IsFaulted)
                {
                    var ex = x.Exception;
                    logger.LogError(ex.Message);
                }
            });

            _tasks.TryAdd(subTaskToken, subTask);
        }

        private async Task HandleStream(
            HttpContext context,
            ConcurrentDictionary<string, CancellationTokenSource> _streams,
            ConcurrentDictionary<string, IRetryableMessage> _ackChannels,
            ConcurrentDictionary<CancellationTokenSource, Task> _tasks,
            WebSocketMessageSender sender,
            string messageJson)
        {
            var subscribe = converter.DeserializeData<StreamInitMessage>(messageJson);

            if (subscribe == null || string.IsNullOrWhiteSpace(subscribe.Id)) return;

            if (_streams.ContainsKey(subscribe.Id)) return;

            var cts = new CancellationTokenSource();
            _streams.TryAdd(subscribe.Id, cts);

            var subTaskToken = new CancellationTokenSource();
            var subTask = Task.Run(async () =>
            {
                try
                {
                    IOperationRequest operationRequest = converter.DeserializeData<OperationRequest>(subscribe.Payload)!;
                    var stream = await entrypoint.HandleMethodStream(operationRequest);

                    StreamingSettings? streamingSettings = GetSettings<StreamingSettingsAttribute>(operationRequest)?.Settings;

                    if (stream == null) { return; }

                    await foreach (var item in stream.WithCancellation(cts.Token))
                    {
                        if (item != null && item.GetType().IsAssignableTo(typeof(IRetryableMessage)))
                        {
                            IRetryableMessage? retryable = item as IRetryableMessage;
                            var ackId = Guid.NewGuid().ToString();
                            _ackChannels.TryAdd(ackId, retryable!);

                            while (await retryable!.CanRetry() && !subTaskToken.IsCancellationRequested)
                            {
                                retryable.GetPayload(out object? message);
                                var edwa = new StreamDataWithAckMessage(subscribe.Id, converter.SerializeToElement(message), ackId);
                                await sender.SendAsync(converter.SerializeToElement(edwa));

                                if (!options.MessageRetryIsEnabled)
                                {
                                    break;
                                }

                            }

                            if (streamingSettings != null && streamingSettings.ThrottleDelay >= TimeSpan.Zero)
                                await Task.Delay(streamingSettings.ThrottleDelay);

                            if (_ackChannels.TryRemove(ackId.ToString(), out IRetryableMessage? channel))
                                await channel.AckAsync();
                        }
                        else
                        {
                            if (!subTaskToken.IsCancellationRequested)
                            {
                                var response = new StreamDataMessage(
                                    subscribe.Id,
                                    converter.SerializeToElement(item)
                                );

                                await sender.SendAsync(response);
                            }
                        }

                        if (streamingSettings != null && streamingSettings.ThrottleDelay >= TimeSpan.Zero)
                            await Task.Delay(streamingSettings.ThrottleDelay);
                    }
                }
                catch (OperationCanceledException ex)
                {
                    // Cancelado normalmente
                }
                catch (Exception ex)
                {
                    await sender.SendAsync(new ErrorMessage(subscribe.Id, ex.Message));
                }
            }).ContinueWith(async x =>
            {
                _tasks.TryRemove(subTaskToken, out _);

                if (x.IsFaulted)
                {
                    var ex = x.Exception;
                    logger.LogError(ex.Message);
                }

                await sender.SendAsync(new StreamCompleteMessage(subscribe.Id));
            });

            _tasks.TryAdd(subTaskToken, subTask);
        }

        private async Task HandlePing(
            WebSocket webSocket,
            WebSocketMessageSender sender,
            string lastPingId,
            string messageJson)
        {
            var pingMessage = converter.DeserializeData<PingMessage>(messageJson);

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
