using Castle.Core.Logging;
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
using Hubcon.Shared.Entrypoint;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Hubcon.Server.Core.Websockets.Middleware
{
    public class HubconWebSocketMiddleware(
        RequestDelegate next, 
        DefaultEntrypoint entrypoint, 
        ILogger<HubconWebSocketMiddleware> logger)
    {
        private int timeoutSeconds = 1000;
        private HeartbeatWatcher _heartbeatWatcher = null!;

        private static readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };


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
            if (!context.WebSockets.IsWebSocketRequest || !(context.Request.Path == "/ws"))
            {
                await next(context);
                return;
            }

            // Si ya hay Authorization header, no hacemos nada
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
                var receiver = new WebSocketMessageReceiver(webSocket);
                var sender = new WebSocketMessageSender(webSocket);

                // Esperar connection_init
                var firstMessageJson = await receiver.ReceiveAsync();

                var initMessage = JsonSerializer.Deserialize<ConnectionInitMessage>(firstMessageJson!, _jsonSerializerOptions);

                if (initMessage == null || initMessage.Type != MessageType.connection_init)
                {
                    var message = $"Se esperaba un mensaje {nameof(MessageType.connection_init)}.";

                    await sender.SendAsync(new ErrorMessage
                    {
                        Error = message
                    });

                    await CloseWebSocketAsync(webSocket, WebSocketCloseStatus.PolicyViolation, message);
                    return;
                }

                await sender.SendAsync(new ConnectionAckMessage());

                var lastPingId = Guid.Empty;

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

                    var baseMessage = JsonSerializer.Deserialize<BaseMessage>(messageJson, _jsonSerializerOptions);

                    if (baseMessage == null) continue;

                    switch (baseMessage.Type)
                    {
                        case MessageType.ping:
                            var ping = HandlePing(webSocket, sender, lastPingId, messageJson);
                            Handle(ping, _tasks);                            
                            break;

                        case MessageType.subscription_init:
                            var subInit = HandleSubscribe(context, _subscriptions, _ackChannels, _tasks, sender, messageJson);
                            Handle(subInit, _tasks);
                            break;

                        case MessageType.subscription_complete:
                            var unsub = HandleUnsubscribe(_subscriptions, context, sender, messageJson);
                            Handle(unsub, _tasks);
                            break;

                        case MessageType.stream_init:
                            var streamInit = HandleStream(context, _streams, _ackChannels, _tasks, sender, messageJson);
                            Handle(streamInit, _tasks);
                            break;

                        case MessageType.stream_complete:
                            var streamComplete = HandleUnsubscribe(_subscriptions, context, sender, messageJson);
                            Handle(streamComplete, _tasks);
                            break;

                        case MessageType.ack:
                            var ack = HandleAck(_ackChannels, messageJson);
                            Handle(ack, _tasks);
                            break;

                        case MessageType.operation_invoke:
                            var operationInvoke = HandleOperationInvoke(context, sender, messageJson);
                            Handle(operationInvoke, _tasks);
                            break;

                        case MessageType.operation_call:
                            var operationCall = HandleOperationCall(context, sender, messageJson);
                            Handle(operationCall, _tasks);
                            break;

                        case MessageType.ingest_init:
                            var ingestInit = HandleIngestInit(_ingests, sender, messageJson);
                            Handle(ingestInit, _tasks);
                            break;

                        case MessageType.ingest_data:
                            var ingestData = HandleIngestData(_ingests, messageJson);
                            Handle(ingestData, _tasks);
                            break;

                        case MessageType.ingest_data_with_ack:
                            var ingestDataWitAck = HandleIngestDataWithAck(_ingests, sender, messageJson);
                            Handle(ingestDataWitAck, _tasks);
                            break;

                        case MessageType.ingest_complete:
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

        private static async Task HandleIngestComplete(ConcurrentDictionary<string, (BaseObservable, CancellationTokenSource, HeartbeatWatcher)> _ingests, string messageJson)
        {
            var ingestCompleteMessage = JsonSerializer.Deserialize<IngestCompleteMessage>(messageJson, _jsonSerializerOptions);

            foreach(var id in ingestCompleteMessage.StreamIds)
            {
                _ingests.TryRemove(id, out var complete);
                await complete.Item3.DisposeAsync();
            }
        }

        private static async Task HandleIngestDataWithAck(ConcurrentDictionary<string, (BaseObservable, CancellationTokenSource, HeartbeatWatcher)> _ingests, WebSocketMessageSender sender, string messageJson)
        {
            var ingestDataWithAckMessage = JsonSerializer.Deserialize<IngestDataWithAckMessage>(messageJson, _jsonSerializerOptions);

            if (ingestDataWithAckMessage == null || !_ingests.TryGetValue(ingestDataWithAckMessage.Id, out var ingestWithAck))
                return;

            ingestWithAck.Item3.NotifyHeartbeat();
            ingestWithAck.Item1.OnNextObject(ingestDataWithAckMessage.Data);

            var ingestDataAckMessage = new IngestDataAckMessage(ingestDataWithAckMessage.Id);
            await sender.SendAsync(ingestDataAckMessage);
        }

        private async Task HandleIngestData(ConcurrentDictionary<string, (BaseObservable, CancellationTokenSource, HeartbeatWatcher)> _ingests, string messageJson)
        {
            var ingestDataMessage = JsonSerializer.Deserialize<IngestDataMessage>(messageJson, _jsonSerializerOptions);

            if (ingestDataMessage == null || !_ingests.TryGetValue(ingestDataMessage.Id, out var ingest))
                return;

            ingest.Item3.NotifyHeartbeat();
            ingest.Item1.OnNextObject(ingestDataMessage.Data);
        }

        private async Task HandleIngestInit(
            ConcurrentDictionary<string, (BaseObservable, CancellationTokenSource, HeartbeatWatcher)> _ingests, 
            WebSocketMessageSender sender, 
            string messageJson)
        {
            var ingestInitMessage = JsonSerializer.Deserialize<IngestInitMessage>(messageJson, _jsonSerializerOptions);

            Dictionary<string, object> sources = new();

            foreach (var id in ingestInitMessage!.StreamIds)
            {
                if (_ingests.TryGetValue(id, out _))
                    return;

                var observable = new GenericObservable<JsonElement>(_jsonSerializerOptions);
                var observer = new AsyncObserver<JsonElement>();
                observable.Subscribe(observer);
                var cts = new CancellationTokenSource();

                var hw = new HeartbeatWatcher(60, async () =>
                {
                    observable.OnCompleted();
                    await cts.CancelAsync();
                    _ingests.TryRemove(id, out var complete);
                    complete.Item2?.Cancel();
                    complete.Item2?.Dispose();
                });

                _ingests.TryAdd(id, (observable, cts, hw));
                sources.TryAdd(id, observer.GetAsyncEnumerable(cts.Token));
            }

            var operationRequest = JsonSerializer.Deserialize<OperationRequest>(ingestInitMessage.Payload, _jsonSerializerOptions)!;
            await sender.SendAsync(new IngestInitAckMessage(ingestInitMessage.Id));
            await entrypoint.HandleIngest(operationRequest, sources);
        }

        public void Handle(Task task, ConcurrentDictionary<CancellationTokenSource, Task> tasks)
        {
            CancellationTokenSource? cts = new CancellationTokenSource();
            var runningTask = Task.Run(() => task, cts.Token);
            tasks.TryAdd(cts, runningTask);
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
                request = JsonSerializer.Deserialize<OperationInvokeMessage>(messageJson, _jsonSerializerOptions)!;

                if (request == null) return;

                try
                {
                    IOperationRequest operationRequest = JsonSerializer.Deserialize<OperationRequest>(request.Payload)!;
                    result = entrypoint.HandleMethodWithResult(operationRequest);
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
                JsonSerializer.SerializeToElement(result, _jsonSerializerOptions)
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
                request = JsonSerializer.Deserialize<OperationInvokeMessage>(messageJson, _jsonSerializerOptions)!;

                if (request == null) return;

                IOperationRequest operationRequest = JsonSerializer.Deserialize<OperationRequest>(request.Payload)!;
                result = entrypoint.HandleMethodWithResult(operationRequest);

                if (context.RequestAborted.IsCancellationRequested)
                    result = false;

                var response = new OperationResponseMessage(
                    request.Id,
                    JsonSerializer.SerializeToElement(result, _jsonSerializerOptions)
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
                request = JsonSerializer.Deserialize<SubscriptionCompleteMessage>(messageJson, _jsonSerializerOptions)!;

                if (request == null) return;

                if (request != null && _subscriptions.TryRemove(request.SubscriptionId, out var tokenSource))
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
                request = JsonSerializer.Deserialize<SubscriptionCompleteMessage>(messageJson, _jsonSerializerOptions)!;

                if (request == null) return;

                if (request != null && _streams.TryRemove(request.SubscriptionId, out var tokenSource))
                    tokenSource.Cancel();
            }
            catch (Exception ex)
            {
                result = false;
                logger.LogError($"{ex.Message}");
            }
        }

        private static async Task HandleAck(
            ConcurrentDictionary<string,
                IRetryableMessage> _ackChannels,
            string messageJson)
        {
            var ack = JsonSerializer.Deserialize<AckMessage>(messageJson)!;

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
            var subscribe = JsonSerializer.Deserialize<SubscriptionInitMessage>(messageJson, _jsonSerializerOptions);

            if (subscribe == null || string.IsNullOrWhiteSpace(subscribe.SubscriptionId)) return;

            if (_subscriptions.ContainsKey(subscribe.SubscriptionId)) return;

            var cts = new CancellationTokenSource();
            _subscriptions.TryAdd(subscribe.SubscriptionId, cts);

            var subTaskToken = new CancellationTokenSource();
            var subTask = Task.Run(async () =>
            {
                try
                {
                    IOperationRequest operationRequest = JsonSerializer.Deserialize<OperationRequest>(subscribe.Payload, _jsonSerializerOptions)!;
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
                                var edwa = new SubscriptionDataWithAckMessage(subscribe.SubscriptionId, JsonSerializer.SerializeToElement(message, _jsonSerializerOptions), ackId);
                                await sender.SendAsync(JsonSerializer.SerializeToElement(edwa, _jsonSerializerOptions));
                            }

                            if (_ackChannels.TryRemove(ackId.ToString(), out IRetryableMessage? channel))
                                await channel.FailedAckAsync();
                        }
                        else
                        {
                            if (!subTaskToken.IsCancellationRequested)
                            {
                                var response = new SubscriptionDataMessage(
                                    subscribe.SubscriptionId,
                                    JsonSerializer.SerializeToElement(item, _jsonSerializerOptions)
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
                    await sender.SendAsync(new ErrorMessage
                    {
                        SubscriptionId = subscribe.SubscriptionId,
                        Error = ex.Message
                    });
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
            var subscribe = JsonSerializer.Deserialize<StreamInitMessage>(messageJson, _jsonSerializerOptions);

            if (subscribe == null || string.IsNullOrWhiteSpace(subscribe.StreamId)) return;

            if (_streams.ContainsKey(subscribe.StreamId)) return;

            var cts = new CancellationTokenSource();
            _streams.TryAdd(subscribe.StreamId, cts);

            var subTaskToken = new CancellationTokenSource();
            var subTask = Task.Run(async () =>
            {
                try
                {
                    IOperationRequest operationRequest = JsonSerializer.Deserialize<OperationRequest>(subscribe.Payload, _jsonSerializerOptions)!;
                    var stream = await entrypoint.HandleMethodStream(operationRequest);

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
                                var edwa = new StreamDataWithAckMessage(subscribe.StreamId, JsonSerializer.SerializeToElement(message, _jsonSerializerOptions), ackId);
                                await sender.SendAsync(JsonSerializer.SerializeToElement(edwa, _jsonSerializerOptions));
                            }

                            if (_ackChannels.TryRemove(ackId.ToString(), out IRetryableMessage? channel))
                                await channel.FailedAckAsync();
                        }
                        else
                        {
                            if (!subTaskToken.IsCancellationRequested)
                            {
                                var response = new StreamDataMessage(
                                    subscribe.StreamId,
                                    JsonSerializer.SerializeToElement(item, _jsonSerializerOptions)
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
                    await sender.SendAsync(new ErrorMessage
                    {
                        SubscriptionId = subscribe.StreamId,
                        Error = ex.Message
                    });
                }
            }).ContinueWith(async x =>
            {
                _tasks.TryRemove(subTaskToken, out _);

                if (x.IsFaulted)
                {
                    var ex = x.Exception;
                    logger.LogError(ex.Message);
                }

                await sender.SendAsync(new StreamCompleteMessage(subscribe.StreamId));
            });

            _tasks.TryAdd(subTaskToken, subTask);
        }

        private async Task HandlePing(
            WebSocket webSocket,
            WebSocketMessageSender sender,
            Guid lastPingId,
            string messageJson)
        {
            var pingMessage = JsonSerializer.Deserialize<PingMessage>(messageJson, _jsonSerializerOptions);

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
