using Castle.Core.Logging;
using Hubcon.Client.Core.Extensions;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Core.Serialization;
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
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Reactive.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;

namespace Hubcon.Client.Core.Websockets
{
    public class HubconWebSocketClient : IAsyncDisposable, IUnsubscriber
    {
        private readonly Uri _uri;
        private readonly IDynamicConverter converter;
        private readonly ILogger<HubconWebSocketClient>? logger;
        private ClientWebSocket? _webSocket;

        public Action<ClientWebSocketOptions>? WebSocketOptions { get; set; }
        public Func<string?>? AuthorizationTokenProvider { get; set; }

        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        private readonly ConcurrentDictionary<string, BaseObservable> _subscriptions = new();
        private readonly ConcurrentDictionary<string, (BaseObservable, CancellationTokenSource, HeartbeatWatcher)> _streams = new();
        private readonly ConcurrentDictionary<string, TaskCompletionSource<IngestDataAckMessage>> _streamDataAck = new();
        private readonly ConcurrentDictionary<string, TaskCompletionSource<IngestInitAckMessage>> _ingestAck = new();
        private readonly ConcurrentDictionary<string, (TaskCompletionSource<IngestCompleteMessage>, CancellationTokenSource)> _ingests = new();
        private readonly ConcurrentDictionary<string, TaskCompletionSource<IngestDataAckMessage>> _ingestDataAck = new();
        private readonly ConcurrentDictionary<string, TaskCompletionSource<OperationResponseMessage>> _operationTcs = new();
        private readonly SemaphoreSlim _sendLock = new(1, 1);
        private readonly SemaphoreSlim _reconnectLock = new(1, 1);

        private readonly CancellationTokenSource _cts = new();
        private CancellationTokenSource? _websocketCts = new();
        private CancellationTokenSource? _receiveLoopCts;
        private CancellationTokenSource? _pingLoopCts;

        private Task? _receiveTask;
        private bool _disposed = false;

        private bool IsReady = false;

        private HeartbeatWatcher? _heartbeatWatcher;

        private readonly GenericObservable<PongMessage> _pongStream;
        private readonly GenericObservable<Exception> _errorStream;

        public IObservable<PongMessage> PongStream => _pongStream;
        public IObservable<Exception> ErrorStream => _errorStream;

        private Task? _pingTask;
        private Task? _timeoutTask;
        private Task? _processingTask;

        private string _lastPongId = Guid.Empty.ToString();
        private DateTime _lastPongTime = DateTime.UtcNow;
        private readonly TimeSpan _timeoutSeconds = TimeSpan.FromSeconds(5);

        private Channel<string> _messageChannel;

        public HubconWebSocketClient(Uri uri, IDynamicConverter converter, ILogger<HubconWebSocketClient>? logger = null)
        {
            _pongStream = new GenericObservable<PongMessage>(_jsonOptions);
            _errorStream = new GenericObservable<Exception>(_jsonOptions);
            _uri = uri;
            this.converter = converter;
            this.logger = logger;
            _messageChannel = Channel.CreateUnbounded<string>();
        }

        public async Task<IObservable<T>> Subscribe<T>(object payload)
        {
            var request = new WebsocketRequest(Guid.NewGuid().ToString(), JsonSerializer.SerializeToElement(payload, _jsonOptions));
            var observable = new GenericObservable<T>(this, request.Id, JsonSerializer.SerializeToElement(request, _jsonOptions), RequestType.Subscription, _jsonOptions);
            if (!_subscriptions.TryAdd(request.Id, observable))
                throw new InvalidOperationException($"Ya existe una suscripción con Id {request.Id}");

            await SendSubscribeMessageAsync(request);
            return observable;
        }

        public async Task<IObservable<T>> Stream<T>(object payload)
        {
            var request = new WebsocketRequest(Guid.NewGuid().ToString(), JsonSerializer.SerializeToElement(payload, _jsonOptions));
            var observable = new GenericObservable<T>(this, request.Id, JsonSerializer.SerializeToElement(request, _jsonOptions), RequestType.Subscription, _jsonOptions);

            var tcs = new CancellationTokenSource();

            tcs.Token.Register(async () =>
            {
                _streams.TryRemove(request.Id, out var obs);
                obs.Item1.OnCompleted();
                await obs.Item3.DisposeAsync();
            });

            var hw = new HeartbeatWatcher(TimeSpan.FromSeconds(5000), async () =>
            {
                if (_streams.TryGetValue(request.Id, out var obs) && obs.Item2.IsCancellationRequested)
                {
                    await obs.Item2.CancelAsync();
                }
            });

            if (!_streams.TryAdd(request.Id, (observable, tcs, hw)))
                throw new InvalidOperationException($"Ya existe un stream con Id {request.Id}");

            await SendStreamMessageAsync(request);
            return observable;
        }

        //public async Task Ingest<T>(IAsyncEnumerable<T> source, object payload, bool needsAck = false)
        //{
        //    var request = new WebsocketRequest(Guid.NewGuid().ToString(), JsonSerializer.SerializeToElement(payload, _jsonOptions));

        //    try
        //    {
        //        var canIngest = await SendIngestMessageAsync(request);

        //        if (!canIngest) throw new TimeoutException("Se excedió el tiempo limite para la confirmación de ingesta al servidor.");

        //        if (needsAck)
        //        {
        //            await foreach (var item in source)
        //            {
        //                var id = Guid.NewGuid().ToString();
        //                var message = new IngestDataMessage(id, JsonSerializer.SerializeToElement(item, _jsonOptions));
        //                var msg = JsonSerializer.Serialize(message);

        //                var tcs = new TaskCompletionSource<IngestDataAckMessage>();
        //                _ingestDataAck.TryAdd(id, tcs);

        //                await SendMessageAsync(msg);

        //                var ackResult = await WaitWithTimeoutAsync(TimeSpan.FromSeconds(5), tcs.Task);

        //                if (ackResult == null) throw new TimeoutException("Se excedió el tiempo limite para la confirmación de ingesta al servidor.");

        //                if (ackResult.Id != request.Id)
        //                {
        //                    throw new InvalidOperationException("La confirmación recibida no coincide con los datos enviados.");
        //                }
        //            }
        //        }
        //        else
        //        {
        //            await foreach (var item in source)
        //            {
        //                var message = new IngestDataWithAckMessage(request.Id, JsonSerializer.SerializeToElement(item, _jsonOptions));
        //                var msg = JsonSerializer.Serialize(message);
        //                await SendMessageAsync(msg);
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        _errorStream.OnNext(ex);
        //        logger?.Error(ex.Message);
        //    }
        //    finally
        //    {
        //        var msg = JsonSerializer.Serialize(new IngestCompleteMessage(request.Id), _jsonOptions);
        //        await SendMessageAsync(msg);
        //    }
        //}

        public async Task IngestMultiple(IOperationRequest payload, bool needsAck = false)
        {
            var cts = new CancellationTokenSource();
            var sourceTasks = new List<Task>();
            var initAckTcs = new TaskCompletionSource<IngestInitAckMessage>();
            var generalTcs = new TaskCompletionSource<IngestCompleteMessage>();
            var sources = new Dictionary<string, IAsyncEnumerable<JsonElement>>();
            var generalId = Guid.NewGuid().ToString();
            var initialAckId = Guid.NewGuid().ToString();
            _ingestAck.TryAdd(initialAckId, initAckTcs);
            _ingests.TryAdd(generalId, (generalTcs, cts));

            try
            {
                foreach(var kvp in payload.Arguments!)
                {
                    if(kvp.Value != null && EnumerableTools.IsAsyncEnumerable(kvp.Value))
                    {
                        var obj = kvp.Value;
                        var id = Guid.NewGuid().ToString();
                        payload.Arguments[kvp.Key] = id;
                        var stream = EnumerableTools.WrapEnumeratorAsJsonElementEnumerable(obj);
                        sources.TryAdd(id, stream!);
                    }
                }

                foreach (var source in sources)
                {
                    var sourceTask = Task.Run(async () =>
                    {
                        var initAckResult = await WaitWithTimeoutAsync(TimeSpan.FromSeconds(15), initAckTcs.Task);

                        if (initAckResult == null)
                        {
                            initAckTcs.TrySetCanceled();
                            throw new TimeoutException("Se excedió el tiempo limite para la confirmación de ingesta al servidor.");
                        }

                        if (initAckResult.Id != initialAckId)
                        {
                            initAckTcs.TrySetCanceled();
                            throw new InvalidOperationException("La confirmación recibida no coincide con los datos enviados.");
                        }

                        await foreach (var item in source.Value.WithCancellation(cts.Token))
                        {
                            //var tcs = new TaskCompletionSource<IngestDataAckMessage>();
                            //_ingestDataAck.TryAdd(source.Key, tcs);

                            if (generalTcs.Task.IsCompleted || cts.IsCancellationRequested)
                                throw new OperationCanceledException("Stream cancelado.");

                            var message = new IngestDataMessage(source.Key, JsonSerializer.SerializeToElement(item, _jsonOptions));
                            var msg = JsonSerializer.Serialize(message, _jsonOptions);

                            await Task.WhenAny(SendMessageAsync(msg, cts.Token), generalTcs.Task);

                            if (generalTcs.Task.IsCompleted || cts.IsCancellationRequested)
                                throw new OperationCanceledException("Stream cancelado.");

                            //var ackResult = await WaitWithTimeoutAsync(TimeSpan.FromSeconds(5), tcs.Task);

                            //if (ackResult == null) 
                            //    throw new TimeoutException("Se excedió el tiempo limite para la confirmación de ingesta al servidor.");

                            //if (ackResult.Id != source.Key)
                            //    throw new InvalidOperationException("La confirmación recibida no coincide con los datos enviados.");               
                        }
                    }, cts.Token);

                    sourceTasks.Add(sourceTask);
                }

                var ingestRequest = new IngestInitMessage(
                    initialAckId,
                    sources.Keys.ToArray(),
                    payload
                );

                var msg = JsonSerializer.Serialize(ingestRequest, _jsonOptions);

                await SendMessageAsync(msg);

                await Task.WhenAll(sourceTasks);
            }
            catch (Exception ex)
            {
                _errorStream.OnNext(ex);
                logger?.LogError(ex.Message);
            }
            finally
            {
                if (IsReady)
                {
                    var msg = JsonSerializer.Serialize(new IngestCompleteMessage(initialAckId, sources.Keys.ToArray()), _jsonOptions);
                    await SendMessageAsync(msg);
                }

                _ingestAck.TryRemove(initialAckId, out var removedCts);
                removedCts?.TrySetCanceled();
                _ingests.TryRemove(generalId, out var removedIngest);
                removedIngest.Item1.TrySetCanceled();
                removedIngest.Item2.Cancel();
            }
        }

        public static class IngestUtils
        {
            public static IAsyncEnumerable<JsonElement> WrapAsJsonElementEnumerable(object value, Type elementType)
            {
                if (value == null)
                    throw new ArgumentNullException(nameof(value));

                if (!typeof(IAsyncEnumerable<>).MakeGenericType(elementType).IsAssignableFrom(value.GetType()))
                    throw new InvalidCastException($"Expected IAsyncEnumerable<{elementType.Name}> but got {value.GetType().Name}");

                var method = typeof(IngestUtils)
                    .GetMethod(nameof(WrapGeneric), BindingFlags.NonPublic | BindingFlags.Static)!
                    .MakeGenericMethod(elementType);

                return (IAsyncEnumerable<JsonElement>)method.Invoke(null, new object[] { value })!;
            }

            private static async IAsyncEnumerable<JsonElement> WrapGeneric<T>(IAsyncEnumerable<T> source)
            {
                await foreach (var item in source)
                {
                    var json = JsonSerializer.SerializeToElement(item);
                    yield return json;
                }
            }
        }

        public async Task SendAsync(object payload)
        {
            var request = new WebsocketRequest(Guid.NewGuid().ToString(), JsonSerializer.SerializeToElement(payload, _jsonOptions));

            var tcs = new TaskCompletionSource<OperationResponseMessage>();
            _operationTcs.TryAdd(request.Id, tcs);

            await SendOperationMessageAsync(request);

            var response = await WaitWithTimeoutAsync(TimeSpan.FromSeconds(5), tcs.Task);

            if (response == null) throw new TimeoutException();

            var result = response == null ? throw new TimeoutException() : JsonSerializer.Deserialize<bool>(response.Result, _jsonOptions)!;

            if (result == true)
                return;
            else
                throw new Exception("Ocurrió un error mientras se ejecutaba la operación.");
        }

        public async Task<T> InvokeAsync<T>(object payload)
        {
            var request = new WebsocketRequest(Guid.NewGuid().ToString(), JsonSerializer.SerializeToElement(payload, _jsonOptions));

            var tcs = new TaskCompletionSource<OperationResponseMessage>();
            _operationTcs.TryAdd(request.Id, tcs);

            await SendOperationMessageAsync(request);

            var response = await WaitWithTimeoutAsync(TimeSpan.FromSeconds(5), tcs.Task);

            return response == null 
                ? throw new TimeoutException("The request timed out.") 
                : JsonSerializer.Deserialize<T>(response.Result, _jsonOptions)!;
        }

        private async Task<Task?> WaitWithTimeoutAsync(TimeSpan timeout, CancellationToken token = default, params Task[] tasks)
        {
            var timeoutTask = Task.Delay(timeout, token);
            var allTasks = Task.WhenAny(tasks);
            var result = await Task.WhenAny(allTasks, timeoutTask);

            if (result == allTasks)
                return allTasks.Result;
            else
                return null;
        }

        private async Task<T> WaitWithTimeoutAsync<T>(TimeSpan timeout, Task<T> task, CancellationToken token = default)
        {
            Task? timeoutTask = null;

            if (timeout == TimeSpan.Zero)
                timeoutTask = Task.Delay(Timeout.Infinite, token);
            else
                timeoutTask = Task.Delay(timeout, token);

            var result = await Task.WhenAny(task, timeoutTask);

            if (result == task)
                return task.Result;
            else
                return default!;
        }

        private async Task HandleIncomingMessage()
        {
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    var json = await _messageChannel.Reader.ReadAsync();
                    var baseMessage = JsonSerializer.Deserialize<BaseMessage>(json);

                    if (baseMessage == null) return;

                    switch (baseMessage?.Type)
                    {
                        case MessageType.pong:
                            var pongMessage = JsonSerializer.Deserialize<PongMessage>(json, _jsonOptions)!;
                            if (_lastPongId == pongMessage.Id)
                            {
                                await _webSocket!.CloseAsync(WebSocketCloseStatus.InvalidPayloadData, "Pong error", default);
                                return;
                            }
                            _lastPongId = pongMessage.Id;
                            _lastPongTime = DateTime.UtcNow;
                            _heartbeatWatcher?.NotifyHeartbeat();
                            _pongStream.OnNext(pongMessage);
                            break;

                        case MessageType.subscription_data:
                            var eventData = JsonSerializer.Deserialize<SubscriptionDataMessage>(json, _jsonOptions);
                            if (eventData?.Id != null && _subscriptions.TryGetValue(eventData.Id, out BaseObservable? sub))
                            {
                                sub.OnNextElement(eventData.Data);
                            }
                            break;

                        case MessageType.stream_data:
                            var streamData = JsonSerializer.Deserialize<StreamDataMessage>(json, _jsonOptions);

                            if (streamData?.Id != null && _streams.TryGetValue(streamData.Id, out var stream))
                            {
                                stream.Item1.OnNextElement(streamData.Data);
                                stream.Item3.NotifyHeartbeat();
                            }

                            break;

                        case MessageType.stream_complete:
                            var streamComplete = JsonSerializer.Deserialize<StreamCompleteMessage>(json, _jsonOptions);

                            if (streamComplete?.Id != null && _streams.TryGetValue(streamComplete.Id, out var streamCompleteInfo))
                            {
                                await streamCompleteInfo.Item2.CancelAsync();
                            }

                            break;

                        case MessageType.error:
                            var errorData = JsonSerializer.Deserialize<ErrorMessage>(json, _jsonOptions);
                            if (errorData?.Id != null && _subscriptions.TryGetValue(errorData.Id, out var subToError))
                            {
                                subToError.OnError(new Exception(errorData.Error));
                            }
                            break;

                        case MessageType.ingest_init_ack:
                            var ingestInitAckMessage = JsonSerializer.Deserialize<IngestInitAckMessage>(json, _jsonOptions);

                            if (ingestInitAckMessage == null) break;

                            if (_ingestAck.TryGetValue(ingestInitAckMessage.Id, out var ingestInitAckTcs))
                            {
                                ingestInitAckTcs.TrySetResult(ingestInitAckMessage);
                            }

                            break;

                        case MessageType.ingest_data_ack:
                            var ingestDataAckMessage = JsonSerializer.Deserialize<IngestDataAckMessage>(json, _jsonOptions);

                            if (ingestDataAckMessage == null) break;

                            if (_ingestDataAck.TryGetValue(ingestDataAckMessage.Id, out var ingestDataAckTcs))
                            {
                                ingestDataAckTcs.TrySetResult(ingestDataAckMessage);
                            }

                            break;

                        case MessageType.operation_response:
                            var operationResponseMessage = JsonSerializer.Deserialize<OperationResponseMessage>(json, _jsonOptions);

                            if (operationResponseMessage == null) break;

                            if (_operationTcs.TryGetValue(operationResponseMessage.Id, out var ormTcs))
                            {
                                ormTcs.TrySetResult(operationResponseMessage);
                            }

                            break;

                        default:
                            var msg = $"Tipo de mensaje no soportado. Tipo recibido: {baseMessage?.Type}";
                            _errorStream.OnNext(new NotSupportedException(msg));
                            logger?.LogError(msg);
                            break;
                    }

                }
            }
            catch (Exception)
            {
            }
        }

        private async Task EnsureConnectedAsync()
        {
            await _reconnectLock.WaitAsync();

            if (_webSocket?.State is WebSocketState.Open or WebSocketState.Connecting)
                return;

            try
            {
                if (_webSocket != null && _webSocket.State == WebSocketState.Open)
                {
                    try
                    {
                        await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Reconnect", CancellationToken.None);
                    }
                    catch { }
                    finally
                    {
                        _webSocket.Dispose();
                    }
                }

                int attempt = 0;
                while (!_cts.IsCancellationRequested)
                {
                    try
                    {
                        IsReady = false;

                        _webSocket = new ClientWebSocket();

                        _websocketCts?.Cancel();
                        _websocketCts?.Dispose();
                        _websocketCts = null;

                        _receiveLoopCts?.Cancel();
                        _receiveLoopCts?.Dispose();
                        _receiveLoopCts = null;

                        _pingLoopCts?.Cancel();
                        _pingLoopCts?.Dispose();
                        _pingLoopCts = null;

                        if (_heartbeatWatcher != null)
                        {
                            await _heartbeatWatcher.DisposeAsync();
                            _heartbeatWatcher = null;
                        }

                        logger?.LogInformation("Intentando conectar...");
                        WebSocketOptions?.Invoke(_webSocket.Options);

                        var uriBuilder = new UriBuilder(_uri);

                        var token = AuthorizationTokenProvider?.Invoke();

                        if (!string.IsNullOrEmpty(token))
                            uriBuilder.AddQueryParameter("access_token", token);

                        await _webSocket.ConnectAsync(uriBuilder.Uri, _cts.Token);

                        logger?.LogInformation("Conectado, intentando handshake...");
                        await SendMessageAsync(JsonSerializer.Serialize(new ConnectionInitMessage(), _jsonOptions));

                        var buffer = new byte[16384];

                        var receiveTask = _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);

                        var connectionResult = await WaitWithTimeoutAsync(TimeSpan.FromSeconds(5), receiveTask);

                        if (connectionResult == null || connectionResult.GetType() != typeof(WebSocketReceiveResult))
                            throw new TimeoutException("Connection failed.");

                        if (connectionResult.MessageType == WebSocketMessageType.Close)
                        {
                            await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server closed", CancellationToken.None);
                            return;
                        }

                        var ack = Encoding.UTF8.GetString(buffer, 0, connectionResult.Count);
                        var ackMessage = JsonSerializer.Deserialize<ConnectionAckMessage>(ack);

                        if (ackMessage?.Type != MessageType.connection_ack)
                            throw new Exception("No se recibió el connection_ack del servidor.");

                        logger?.LogInformation("Confirmación recibida, iniciando loop de respuesta...");

                        _websocketCts = new CancellationTokenSource();
                        _receiveLoopCts = new CancellationTokenSource();
                        _pingLoopCts = new CancellationTokenSource();

                        _timeoutTask ??= Task.Run(ReconnectLoop);
                        //await _timeoutTask.ConfigureAwait(false);

                        _processingTask ??= Task.Run(HandleIncomingMessage);
                        //await _processingTask.ConfigureAwait(false);

                        _pingTask = Task.Run(() => PingMessageLoop(_pingLoopCts.Token));
                        //await _pingTask.ConfigureAwait(false);

                        _heartbeatWatcher = new HeartbeatWatcher(_timeoutSeconds, async () =>
                        {
                            IsReady = false;
                            logger?.LogInformation("Socket timed out.");
                            await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Socket timed out.", default);
                        });

                        _receiveTask = Task.Run(() => ReceiveLoopAsync(_receiveLoopCts.Token));
                        //await _receiveTask.ConfigureAwait(false);

                        foreach (var kvp in _subscriptions.Values)
                            await SendSubscribeMessageAsync(JsonSerializer.Deserialize<WebsocketRequest>(kvp.RequestData!.Request, _jsonOptions)!);

                        IsReady = true;

                        return;
                    }
                    catch (Exception ex)
                    {
                        _errorStream.OnNext(ex);
                        logger?.LogError(ex.Message);
                        int delay = Math.Min(1 * ++attempt, 10);
                        logger?.LogInformation($"Reconectando en {delay} segundos...");
                        await Task.Delay(delay * 1000, _cts.Token);

                        foreach(var item in _ingests)
                        {
                            _ingests.TryRemove(item);
                            item.Value.Item1.TrySetCanceled();
                            await item.Value.Item2.CancelAsync();
                        }

                        foreach(var item in _ingestAck)
                        {
                            _ingestAck.TryRemove(item);
                            item.Value.TrySetCanceled();
                        }

                        foreach(var item in _ingestDataAck)
                        {
                            _ingestDataAck.TryRemove(item);
                            item.Value.TrySetCanceled();
                        }
                    }
                }
            }
            finally
            {
                _reconnectLock.Release();
            }
        }

        //private async Task<bool> SendIngestMessageAsync(IEnumerable<string> streamIds)
        //{
        //    var msg = JsonSerializer.Serialize(new IngestInitMessage(streamIds, request.Payload), _jsonOptions);
        //    var cts = new TaskCompletionSource<IngestInitAckMessage>();
        //    _ingestAck.TryAdd(request.Id, cts);
        //    await SendMessageAsync(msg);

        //    var result = await WaitWithTimeoutAsync(TimeSpan.FromSeconds(5), _cts.Token, cts.Task);

        //    _ingestAck.TryRemove(request.Id, out _);

        //    if (result != null)
        //    {
        //        var ackMessage = cts.Task.Result;

        //        if (ackMessage.Id != null && ackMessage.Id == request.Id)
        //        {
        //            return true;
        //        }

        //        return false;
        //    }
        //    else
        //        return false;
        //}

        private async Task SendSubscribeMessageAsync(WebsocketRequest request)
        {
            var msg = JsonSerializer.Serialize(new SubscriptionInitMessage(request.Id, request.Payload), _jsonOptions);
            await SendMessageAsync(msg);
        }

        private async Task SendStreamMessageAsync(WebsocketRequest request)
        {
            var msg = JsonSerializer.Serialize(new StreamInitMessage(request.Id, request.Payload), _jsonOptions);
            await SendMessageAsync(msg);
        }


        private async Task SendUnsubscribeMessageAsync(string subscriptionId)
        {
            var msg = JsonSerializer.Serialize(new SubscriptionCompleteMessage(subscriptionId), _jsonOptions);
            await SendMessageAsync(msg);
        }

        private async Task SendOperationMessageAsync(WebsocketRequest request)
        {
            var msg = JsonSerializer.Serialize(new OperationInvokeMessage(request.Id, request.Payload), _jsonOptions);
            await SendMessageAsync(msg);
        }

        private async Task SendCallOperationMessageAsync(WebsocketRequest request)
        {
            var msg = JsonSerializer.Serialize(new OperationCallMessage(request.Id, request.Payload), _jsonOptions);
            await SendMessageAsync(msg);
        }

        private async Task SendMessageAsync(string msg, CancellationToken? cancellationToken = null)
        {
            if (_webSocket?.State != WebSocketState.Open)
                await EnsureConnectedAsync();

            var buffer = Encoding.UTF8.GetBytes(msg);

            await _sendLock.WaitAsync(_cts.Token);
            try
            {
                await _webSocket!.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, cancellationToken ?? _cts.Token);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        private async Task PingMessageLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (_webSocket?.State == WebSocketState.Open && IsReady == true)
                        await SendMessageAsync(JsonSerializer.Serialize(new PingMessage(Guid.NewGuid().ToString()), _jsonOptions));

                    await Task.Delay(4000, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger?.LogError($"Error en PingMessageLoop: {ex.Message}");
                }
            }
        }

        int cantidad = 0;
        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[16384];
            Interlocked.Increment(ref cantidad);
            logger?.LogInformation($"ReceiveLoop iniciado. Cantidad: {Volatile.Read(ref cantidad)}");

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        if (_webSocket == null) break;

                        var sb = new StringBuilder();
                        WebSocketReceiveResult result;

                        do
                        {
                            var receiveTask = _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                            result = await WaitWithTimeoutAsync(TimeSpan.Zero, receiveTask);

                            cancellationToken.ThrowIfCancellationRequested();

                            if (result.MessageType == WebSocketMessageType.Close)
                            {
                                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server closed", CancellationToken.None);
                                throw new OperationCanceledException();
                            }

                            sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                        } while (!result.EndOfMessage);

                        await _messageChannel.Writer.WriteAsync(sb.ToString(), cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex.ToString());
                        break;
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _errorStream.OnNext(ex);
                logger?.LogError("Error en ReceiveLoop: " + ex.Message);
            }
            finally
            {
                logger?.LogInformation($"ReceiveLoop terminado. Cantidad: {Volatile.Read(ref cantidad)}");
                Interlocked.Decrement(ref cantidad);
            }
        }
        private async Task ReconnectLoop()
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(5000);

                    if (_webSocket?.State != WebSocketState.Open && _webSocket?.State != WebSocketState.Connecting)
                    {
                        logger?.LogInformation("WebSocket no está abierto. Reconectando...");
                        await EnsureConnectedAsync();
                    }
                }
                catch (Exception ex)
                {
                    logger?.LogError($"Error en ReconnectLoop: {ex.Message}");
                }
            }
        }
        public async Task Unsubscribe(IRequest request)
        {
            try
            {
                switch (request.Type)
                {
                    case RequestType.Subscription:
                        await SendUnsubscribeMessageAsync(request.Id);
                        break;
                }
            }
            catch (Exception ex)
            {
                _errorStream.OnNext(ex);
            }
        }
        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;

            _disposed = true;
            _cts.Cancel();

            try
            {
                _webSocket?.Abort();
                _webSocket?.Dispose();
                _receiveLoopCts?.Cancel();
                _receiveLoopCts?.Dispose();

                await Task.WhenAll(_pingTask ?? Task.CompletedTask, _timeoutTask ?? Task.CompletedTask);
            }
            catch { }
        }
    }
}
