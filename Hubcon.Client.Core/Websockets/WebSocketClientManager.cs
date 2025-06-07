using Hubcon.Shared.Core.Websockets;
using Hubcon.Shared.Core.Websockets.Events;
using Hubcon.Shared.Core.Websockets.Heartbeat;
using Hubcon.Shared.Core.Websockets.Interfaces;
using Hubcon.Shared.Core.Websockets.Messages.Connection;
using Hubcon.Shared.Core.Websockets.Messages.Generic;
using Hubcon.Shared.Core.Websockets.Messages.Ingest;
using Hubcon.Shared.Core.Websockets.Messages.Operation;
using Hubcon.Shared.Core.Websockets.Messages.Ping;
using Hubcon.Shared.Core.Websockets.Messages.Subscriptions;
using Hubcon.Shared.Core.Websockets.Models;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;

namespace Hubcon.Client.Core.Websockets
{
    public class WebSocketClient : IAsyncDisposable, IUnsubscriber
    {
        private readonly Uri _uri;
        private ClientWebSocket? _webSocket;
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        private readonly ConcurrentDictionary<string, BaseObservable> _subscriptions = new();
        private readonly ConcurrentDictionary<string, TaskCompletionSource<IngestInitAckMessage>> _ingestAck = new();
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

        private Guid _lastPongId = Guid.Empty;
        private DateTime _lastPongTime = DateTime.UtcNow;
        private const int _timeoutSeconds = 10;

        private Channel<string> _messageChannel;

        public WebSocketClient(Uri uri)
        {
            _pongStream = new GenericObservable<PongMessage>(_jsonOptions);
            _errorStream = new GenericObservable<Exception>(_jsonOptions);
            _uri = uri;
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

        public async Task Ingest<T>(IAsyncEnumerable<T> source, object payload, bool needsAck = false)
        {
            var request = new WebsocketRequest(Guid.NewGuid().ToString(), JsonSerializer.SerializeToElement(payload, _jsonOptions));

            try
            {
                var canIngest = await SendIngestMessageAsync(request);

                if (!canIngest) throw new TimeoutException("Se excedió el tiempo limite para la confirmación de ingesta al servidor.");

                if (needsAck)
                {
                    await foreach (var item in source)
                    {
                        var id = Guid.NewGuid().ToString();
                        var message = new IngestDataMessage(id, JsonSerializer.SerializeToElement(item, _jsonOptions));
                        var msg = JsonSerializer.Serialize(message);

                        var tcs = new TaskCompletionSource<IngestDataAckMessage>();
                        _ingestDataAck.TryAdd(id, tcs);

                        await SendMessageAsync(msg);

                        var ackResult = await WaitWithTimeoutAsync(TimeSpan.FromSeconds(5), _cts.Token, tcs.Task);

                        if (ackResult == null) throw new TimeoutException("Se excedió el tiempo limite para la confirmación de ingesta al servidor.");

                        if (ackResult.Id != request.Id)
                        {
                            throw new InvalidOperationException("La confirmación recibida no coincide con los datos enviados.");
                        }
                    }
                }
                else
                {
                    await foreach (var item in source)
                    {
                        var message = new IngestDataWithAckMessage(request.Id, JsonSerializer.SerializeToElement(item, _jsonOptions));
                        var msg = JsonSerializer.Serialize(message);
                        await SendMessageAsync(msg);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            finally
            {
                var msg = JsonSerializer.Serialize(new IngestCompleteMessage(request.Id), _jsonOptions);
                await SendMessageAsync(msg);
            }
        }

        public async Task SendAsync(object payload)
        {
            var request = new WebsocketRequest(Guid.NewGuid().ToString(), JsonSerializer.SerializeToElement(payload, _jsonOptions));

            var tcs = new TaskCompletionSource<OperationResponseMessage>();
            _operationTcs.TryAdd(request.Id, tcs);

            await SendOperationMessageAsync(request);

            var response = await WaitWithTimeoutAsync(TimeSpan.FromSeconds(5), _cts.Token, tcs.Task);

            if (response == null) throw new TimeoutException();

            var result =  response == null ? throw new TimeoutException() : JsonSerializer.Deserialize<bool>(response.Result, _jsonOptions)!;

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

            var response = await WaitWithTimeoutAsync(TimeSpan.FromSeconds(5), _cts.Token, tcs.Task);

            return response == null ? throw new TimeoutException() : JsonSerializer.Deserialize<T>(response.Result, _jsonOptions)!;
        }

        private async Task<Task?> WaitWithTimeoutAsync(TimeSpan timeout, CancellationToken token, params Task[] tasks)
        {
            var timeoutTask = Task.Delay(timeout, token);
            var allTasks = Task.WhenAny(tasks);
            var result = await Task.WhenAny(allTasks, timeoutTask);

            if (result == allTasks)
                return allTasks.Result;
            else
                return null;
        }

        private async Task<T> WaitWithTimeoutAsync<T>(TimeSpan timeout, CancellationToken token, Task<T> task)
        {
            Task? timeoutTask = null;  
            
            if(timeout == TimeSpan.Zero)
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
                    var json = await _messageChannel.Reader.ReadAsync(_cts.Token);
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

                        case MessageType.subscription_event_data:
                            var eventData = JsonSerializer.Deserialize<EventDataMessage>(json, _jsonOptions);
                            if (eventData?.Id != null && _subscriptions.TryGetValue(eventData.Id, out BaseObservable? sub))
                            {
                                sub.OnNextElement(eventData.Data);
                            }
                            break;

                        case MessageType.error:
                            var errorData = JsonSerializer.Deserialize<ErrorMessage>(json, _jsonOptions);
                            if (errorData?.SubscriptionId != null && _subscriptions.TryGetValue(errorData.SubscriptionId, out var subToError))
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
                            Console.WriteLine(msg);
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

                        Console.WriteLine("Intentando conectar...");
                        await _webSocket.ConnectAsync(_uri, _cts.Token);

                        Console.WriteLine("Conectado, intentando handshake...");
                        await SendMessageAsync(JsonSerializer.Serialize(new ConnectionInitMessage(), _jsonOptions));

                        var buffer = new byte[8192];

                        var receiveTask = _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                        var shortCircuitTask = Task.Delay(5000);

                        var connectionResult = await WaitWithTimeoutAsync(TimeSpan.FromSeconds(5), _cts.Token, receiveTask);

                        if (connectionResult is null || connectionResult is not WebSocketReceiveResult)
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

                        Console.WriteLine("Confirmación recibida, iniciando loop de respuesta...");

                        _websocketCts = new CancellationTokenSource();
                        _receiveLoopCts = new CancellationTokenSource();
                        _pingLoopCts = new CancellationTokenSource();

                        _timeoutTask ??= Task.Run(ReconnectLoop);
                        _processingTask ??= Task.Run(HandleIncomingMessage);
                        _pingTask = Task.Run(() => PingMessageLoop(_pingLoopCts.Token));

                        _heartbeatWatcher = new HeartbeatWatcher(_timeoutSeconds, async () =>
                        {
                            IsReady = false;
                            Console.WriteLine("Socket timed out.");
                            await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Socket timed out.", default);
                        });

                        _receiveTask = Task.Run(() => ReceiveLoopAsync(_receiveLoopCts.Token));

                        foreach (var kvp in _subscriptions.Values)
                            await SendSubscribeMessageAsync(JsonSerializer.Deserialize<WebsocketRequest>(kvp.RequestData!.Request, _jsonOptions)!);

                        IsReady = true;

                        return;
                    }
                    catch (Exception ex)
                    {
                        _errorStream.OnNext(ex);
                        Console.WriteLine(ex.Message);
                        int delay = Math.Min(1 * ++attempt, 10);
                        Console.WriteLine($"Reconectando en {delay} segundos...");
                        await Task.Delay(delay * 1000, _cts.Token);
                    }
                }
            }
            finally
            {
                _reconnectLock.Release();
            }
        }

        private async Task<bool> SendIngestMessageAsync(WebsocketRequest request)
        {
            var msg = JsonSerializer.Serialize(new IngestInitMessage(request.Id), _jsonOptions);
            var cts = new TaskCompletionSource<IngestInitAckMessage>();
            _ingestAck.TryAdd(request.Id, cts);
            await SendMessageAsync(msg);

            var result = await WaitWithTimeoutAsync(TimeSpan.FromSeconds(5), _cts.Token, cts.Task);

            _ingestAck.TryRemove(request.Id, out _);

            if (result != null)
            {
                var ackMessage = cts.Task.Result;

                if (ackMessage.Id != null && ackMessage.Id == request.Id)
                {
                    return true;
                }

                return false;
            }
            else
                return false;
        }

        private async Task SendSubscribeMessageAsync(WebsocketRequest request)
        {
            var msg = JsonSerializer.Serialize(new SubscribeMessage(request.Id, request.Payload), _jsonOptions);
            await SendMessageAsync(msg);
        }

        private async Task SendUnsubscribeMessageAsync(string subscriptionId)
        {
            var msg = JsonSerializer.Serialize(new UnsubscribeMessage(subscriptionId), _jsonOptions);
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

        private async Task SendMessageAsync(string msg)
        {
            if (_webSocket?.State != WebSocketState.Open)
                await EnsureConnectedAsync();

            var buffer = Encoding.UTF8.GetBytes(msg);

            await _sendLock.WaitAsync(_cts.Token);
            try
            {
                await _webSocket!.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, _cts.Token);
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
                        await SendMessageAsync(JsonSerializer.Serialize(new PingMessage(), _jsonOptions));

                    await Task.Delay(4000, cancellationToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error en PingMessageLoop: {ex.Message}");
                }
            }
        }

        int cantidad = 0;
        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[8192];
            Interlocked.Increment(ref cantidad);
            Console.WriteLine($"ReceiveLoop iniciado. Cantidad: {Volatile.Read(ref cantidad)}");

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
                            result = await WaitWithTimeoutAsync(TimeSpan.Zero, _cts.Token, receiveTask);

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
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _errorStream.OnNext(ex);
                Console.WriteLine("Error en ReceiveLoop: " + ex.Message);
            }
            finally
            {
                Console.WriteLine($"ReceiveLoop terminado. Cantidad: {Volatile.Read(ref cantidad)}");
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
                        Console.WriteLine("WebSocket no está abierto. Reconectando...");
                        await EnsureConnectedAsync();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error en ReconnectLoop: {ex.Message}");
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
