using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Client.Core.Exceptions;
using Hubcon.Client.Core.Extensions;
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
using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Reactive.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace Hubcon.Client.Core.Websockets
{
    public class HubconWebSocketClient : IAsyncDisposable, IUnsubscriber
    {
        private readonly Uri _uri;
        private readonly IDynamicConverter converter;
        private readonly IClientOptions options;
        private readonly ILogger<HubconWebSocketClient>? logger;
        private ClientWebSocket? _webSocket;

        public bool LoggingEnabled { get; set; } = true;

        public Action<ClientWebSocketOptions>? WebSocketOptions { get; set; }
        public Func<string?>? AuthorizationTokenProvider { get; set; }

        private readonly ConcurrentDictionary<Guid, BaseObservable> _subscriptions = new();
        private readonly ConcurrentDictionary<Guid, (BaseObservable, CancellationTokenSource, HeartbeatWatcher)> _streams = new();
        private readonly ConcurrentDictionary<Guid, TaskCompletionSource<IngestDataAckMessage>> _streamDataAck = new();
        private readonly ConcurrentDictionary<Guid, TaskCompletionSource<IngestInitAckMessage>> _ingestAck = new();
        private readonly ConcurrentDictionary<Guid, (TaskCompletionSource<IngestResultMessage>, CancellationTokenSource)> _ingests = new();
        private readonly ConcurrentDictionary<Guid, TaskCompletionSource<IngestDataAckMessage>> _ingestDataAck = new();
        private readonly ConcurrentDictionary<Guid, TaskCompletionSource<OperationResponseMessage>> _operationTcs = new();
        private readonly SemaphoreSlim _reconnectLock = new(1, 1);

        private readonly CancellationTokenSource _cts = new();
        private CancellationTokenSource? _websocketCts = new();
        private CancellationTokenSource? _receiveLoopCts;
        private CancellationTokenSource? _pingLoopCts;
        private CancellationTokenSource? _sendLoopCts;

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
        private Task? _receiveTask;
        private Task? _sendTask;

        private Guid _lastPongId = Guid.Empty;
        private DateTime _lastPongTime = DateTime.UtcNow;

        private readonly Channel<TrimmedMemoryOwner> _messageChannel;
        private readonly Channel<byte[]> _sendChannel;

        public HubconWebSocketClient(Uri uri, IDynamicConverter converter, IClientOptions options, ILogger<HubconWebSocketClient>? logger = null)
        {
            _pongStream = new GenericObservable<PongMessage>(converter);
            _errorStream = new GenericObservable<Exception>(converter);
            _uri = uri;
            this.converter = converter;
            this.options = options;
            this.logger = logger;

            _messageChannel = Channel.CreateBounded<TrimmedMemoryOwner>(new BoundedChannelOptions(40000)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleWriter = true,
                SingleReader = false
            });

            _sendChannel = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(20000)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleWriter = false,
                SingleReader = true
            });
        }

        public async Task<IObservable<T>> Subscribe<T>(IOperationRequest payload)
        {
            var request = new SubscriptionInitMessage(Guid.NewGuid(), converter.SerializeToElement(payload));

            var observable = new GenericObservable<T>(this, request.Id, converter.SerializeToElement(request), RequestType.Subscription, converter, options.ReconnectSubscriptions);

            if (!_subscriptions.TryAdd(request.Id, observable))
                throw new InvalidOperationException($"Ya existe una suscripción con Id {request.Id}");

            if (_webSocket?.State != WebSocketState.Open)
                await EnsureConnectedAsync();

            await SendMessageAsync(request);

            return observable;
        }

        public async Task<IObservable<T>> Stream<T>(IOperationRequest payload)
        {
            var request = new StreamInitMessage(Guid.NewGuid(), converter.SerializeToElement(payload));

            var observable = new GenericObservable<T>(this, request.Id, converter.SerializeToElement(request), RequestType.Subscription, converter, options.ReconnectStreams);
            var tcs = new CancellationTokenSource();

            if (_webSocket?.State != WebSocketState.Open)
                await EnsureConnectedAsync();

            tcs.Token.Register(async () =>
            {
                _streams.TryRemove(request.Id, out var obs);
                obs.Item1.OnCompleted();
                await obs.Item3.DisposeAsync();
            });

            var hw = new HeartbeatWatcher(TimeSpan.FromSeconds(15000), async () =>
            {
                if (_streams.TryGetValue(request.Id, out var obs) && obs.Item2.IsCancellationRequested)
                {
                    await obs.Item2.CancelAsync();
                }
            });

            if (!_streams.TryAdd(request.Id, (observable, tcs, hw)))
                throw new InvalidOperationException($"Ya existe un stream con Id {request.Id}");

            await SendMessageAsync(request);
            return observable;
        }


        public async Task<IOperationResponse<T>> IngestMultiple<T>(IOperationRequest payload, bool needsAck = false)
        {
            using var cts = new CancellationTokenSource();
            var sourceTasks = new List<Task>();
            var initAckTcs = new TaskCompletionSource<IngestInitAckMessage>();
            var generalTcs = new TaskCompletionSource<IngestResultMessage>();
            var sources = new Dictionary<Guid, IAsyncEnumerable<JsonElement>>();
            var initialAckId = Guid.NewGuid();
            _ingestAck.TryAdd(initialAckId, initAckTcs);
            _ingests.TryAdd(initialAckId, (generalTcs, cts));

            if (_webSocket?.State != WebSocketState.Open)
                await EnsureConnectedAsync();

            try
            {
                foreach (var kvp in payload.Arguments!)
                {
                    if (kvp.Value != null && EnumerableTools.IsAsyncEnumerable(kvp.Value))
                    {
                        var obj = kvp.Value;
                        var id = Guid.NewGuid();
                        payload.Arguments[kvp.Key] = id;
                        var stream = EnumerableTools.WrapEnumeratorAsJsonElementEnumerable(obj);
                        sources.TryAdd(id, stream!);
                    }
                }

                foreach (var source in sources)
                {
                    var sourceTask = Task.Run(async () =>
                    {
                        try
                        {
                            var initAckResult = await TimeoutHelper.WaitWithTimeoutAsync(initAckTcs.Task.WaitAsync, TimeSpan.FromSeconds(15));
                            if (initAckResult == null || initAckResult.Id != initialAckId)
                                throw new TimeoutException("Timeout o ID incorrecto en IngestInitAck");

                            await foreach (var item in source.Value.WithCancellation(cts.Token))
                            {
                                if (generalTcs.Task.IsCompleted || cts.IsCancellationRequested)
                                    break;

                                var message = new IngestDataMessage(source.Key, converter.SerializeToElement(item));

                                try
                                {
                                    await SendMessageAsync(message, cts.Token);
                                }
                                catch (Exception ex)
                                {
                                    if (LoggingEnabled)
                                        logger?.LogError(ex, $"Error al enviar dato en ingest stream {source.Key}");
                                    _errorStream.OnNext(ex);
                                }

                                if (generalTcs.Task.IsCompleted || cts.IsCancellationRequested)
                                    break;
                            }
                        }
                        catch (Exception ex)
                        {
                            if (LoggingEnabled)
                                logger?.LogError(ex, $"Error en ingest stream {source.Key}");
                            _errorStream.OnNext(ex);
                            cts.Cancel();
                        }
                    }, cts.Token);

                    sourceTasks.Add(sourceTask);
                }

                var ingestRequest = new IngestInitMessage(initialAckId, sources.Keys.ToArray(), converter.SerializeToElement(payload));

                try
                {
                    await SendMessageAsync(ingestRequest);
                }
                catch (Exception ex)
                {
                    if (LoggingEnabled)
                        logger?.LogError(ex, "Error al enviar IngestInitMessage");

                    _errorStream.OnNext(ex);
                    cts.Cancel();
                }

                var allIngests = Task.WhenAll(sourceTasks);

                await Task.WhenAny(allIngests, generalTcs.Task);

                await SendMessageAsync(new IngestCompleteMessage(initialAckId, sources.Keys.ToArray()));

                var result = await TimeoutHelper.WaitWithTimeoutAsync(generalTcs.Task.WaitAsync, TimeSpan.FromSeconds(15));

                if (result == null) throw new HubconRemoteException("Received an empty response.");

                var response = converter.DeserializeJsonElement<BaseOperationResponse<T>>(result.Data)
                    ?? throw new HubconRemoteException("Received an empty response.");

                if (!response.Success)
                    throw new HubconRemoteException(response.Error);

                return response;
            }
            catch (Exception ex)
            {
                if (LoggingEnabled)
                    logger?.LogError(ex, "Error general en IngestMultiple");

                _errorStream.OnNext(ex);

                throw new HubconGenericException(ex.Message, ex);
            }
            finally
            {
                if (IsReady && !cts.IsCancellationRequested)
                {
                    var msg = new IngestCompleteMessage(initialAckId, sources.Keys.ToArray());
                    await SendMessageAsync(msg);
                }

                _ingestAck.TryRemove(initialAckId, out var removedCts);
                removedCts?.TrySetCanceled();
                _ingests.TryRemove(initialAckId, out var removedIngest);
                removedIngest.Item1?.TrySetCanceled();
                removedIngest.Item2?.Cancel();
            }
        }

        public async Task SendAsync(IOperationRequest payload)
        {
            var request = new OperationCallMessage(Guid.NewGuid(), converter.SerializeToElement(payload));

            if (_webSocket?.State != WebSocketState.Open)
                await EnsureConnectedAsync();

            await SendMessageAsync(request);
        }

        public async ValueTask<IOperationResponse<T>> InvokeAsync<T>(IOperationRequest payload, IContractOptions? contractOptions = null)
        {
            var request = new OperationInvokeMessage(Guid.NewGuid(), converter.SerializeToElement(payload));
            var tcs = new TaskCompletionSource<OperationResponseMessage>();
            _operationTcs.TryAdd(request.Id, tcs);
            OperationResponseMessage? response = null;

            if (_webSocket?.State != WebSocketState.Open)
                await EnsureConnectedAsync();

            try
            {
                await SendMessageAsync(request);

                response = await TimeoutHelper.WaitWithTimeoutAsync(tcs.Task.WaitAsync, TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                if (LoggingEnabled)
                    logger?.LogError(ex.Message);

                _errorStream.OnNext(ex);
            }
            finally
            {
                _operationTcs.TryRemove(request.Id, out _);
            }

            if (response == null)
                throw new HubconGenericException("There was an unknown error or the request timed out.");

            var converted = converter.DeserializeJsonElement<BaseOperationResponse<T>>(response.Result)!;

            if (!converted.Success)
                throw new HubconRemoteException(converted.Error ?? "An error occurred on the server while processing the request.");

            return converted;
        }

        private async Task HandleIncomingMessage()
        {
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    if (_webSocket?.State != WebSocketState.Open)
                        await EnsureConnectedAsync();

                    TrimmedMemoryOwner? tmo = await _messageChannel.Reader.ReadAsync();

                    var message = new BaseMessage(tmo.Memory);

                    if (message.Id == Guid.Empty)
                        continue;

                    //if (!JsonHelper.TryGetEnumFromJson(json, "type", out MessageType type, out JsonElement element))
                    //    continue;

                    switch (message.Type)
                    {
                        case MessageType.pong:
                            if (!options.WebsocketRequiresPong)
                                break;

                            var pongMessage = new PongMessage(tmo.Memory)!;

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
                            var eventData = new SubscriptionDataMessage(tmo.Memory);
                            if (eventData?.Id != null && _subscriptions.TryGetValue(eventData.Id, out BaseObservable? sub))
                            {
                                sub.OnNextElement(eventData.Data);
                            }
                            break;

                        case MessageType.stream_data:
                            var streamData = new StreamDataMessage(tmo.Memory);

                            if (streamData?.Id != null && _streams.TryGetValue(streamData.Id, out var stream))
                            {
                                stream.Item1.OnNextElement(streamData.Data);
                                stream.Item3.NotifyHeartbeat();
                            }

                            break;

                        case MessageType.stream_complete:
                            var streamComplete = new StreamCompleteMessage(tmo.Memory);

                            if (streamComplete?.Id != null && _streams.TryGetValue(streamComplete.Id, out var streamCompleteInfo))
                            {
                                await streamCompleteInfo.Item2.CancelAsync();
                            }

                            break;

                        case MessageType.error:
                            var errorData = new ErrorMessage(tmo.Memory);
                            if (errorData?.Id != null && _subscriptions.TryGetValue(errorData.Id, out var subToError))
                            {
                                subToError.OnError(new Exception(errorData.Error));
                            }
                            break;

                        case MessageType.ingest_init_ack:
                            var ingestInitAckMessage = new IngestInitAckMessage(tmo.Memory);

                            if (ingestInitAckMessage == null) break;

                            if (_ingestAck.TryGetValue(ingestInitAckMessage.Id, out var ingestInitAckTcs))
                            {
                                ingestInitAckTcs.TrySetResult(ingestInitAckMessage);
                            }

                            break;

                        case MessageType.ingest_result:
                            var ingestResultMessage = new IngestResultMessage(tmo.Memory);

                            if (ingestResultMessage == null) break;

                            if (_ingests.TryGetValue(ingestResultMessage.Id, out var ingestResultMessageTcs))
                            {
                                ingestResultMessageTcs.Item1.TrySetResult(ingestResultMessage);
                            }

                            break;

                        case MessageType.ingest_data_ack:
                            var ingestDataAckMessage = new IngestDataAckMessage(tmo.Memory);

                            if (ingestDataAckMessage == null) break;

                            if (_ingestDataAck.TryGetValue(ingestDataAckMessage.Id, out var ingestDataAckTcs))
                            {
                                ingestDataAckTcs.TrySetResult(ingestDataAckMessage);
                            }

                            break;

                        case MessageType.operation_response:
                            var operationResponseMessage = new OperationResponseMessage(tmo.Memory);

                            if (operationResponseMessage == null) break;

                            if (_operationTcs.TryGetValue(operationResponseMessage.Id, out var ormTcs))
                            {
                                ormTcs.TrySetResult(operationResponseMessage);
                            }

                            break;

                        default:
                            var msg = $"Tipo de mensaje no soportado. Tipo recibido: {message.Type.ToString()}";
                            _errorStream.OnNext(new HubconGenericException(msg));

                            if (LoggingEnabled)
                                logger?.LogError(msg);

                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                if (LoggingEnabled)
                    logger?.LogError($"Error en HandleIncomingMessage: {ex.Message}");

                _errorStream.OnNext(ex);
            }
            finally
            {
                // Do nothing, for now.
            }
        }

        private async Task EnsureConnectedAsync()
        {
            await _reconnectLock.WaitAsync();

            try
            {
                if (_webSocket?.State is WebSocketState.Open or WebSocketState.Connecting)
                    return;

                if (_webSocket != null && _webSocket.State == WebSocketState.Open)
                {
                    try
                    {
                        await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Reconnect", CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        if (LoggingEnabled)
                            logger?.LogError(ex, ex.Message);

                        _errorStream.OnNext(ex);
                    }
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

                        _sendLoopCts?.Cancel();
                        _sendLoopCts?.Dispose();
                        _sendLoopCts = null;
                        _sendLoopCts = new CancellationTokenSource();
                        _sendTask = Task.Run(() => SendLoopAsync(_webSocket, _sendLoopCts.Token));

                        if (_heartbeatWatcher != null)
                        {
                            await _heartbeatWatcher.DisposeAsync();
                            _heartbeatWatcher = null;
                        }

                        if (LoggingEnabled)
                            logger?.LogInformation("Intentando conectar...");

                        WebSocketOptions?.Invoke(_webSocket.Options);

                        var uriBuilder = new UriBuilder(_uri);

                        var token = AuthorizationTokenProvider?.Invoke();

                        if (!string.IsNullOrEmpty(token))
                            uriBuilder.AddQueryParameter("access_token", token);

                        await _webSocket.ConnectAsync(uriBuilder.Uri, _cts.Token);

                        if (LoggingEnabled)
                            logger?.LogInformation("Conectado, intentando handshake...");

                        await SendMessageAsync(new ConnectionInitMessage(Guid.NewGuid()));

                        var buffer = new byte[16384];

                        var receiveTask = _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);

                        var connectionResult = await TimeoutHelper.WaitWithTimeoutAsync(receiveTask.WaitAsync, TimeSpan.FromSeconds(5));

                        if (connectionResult == null || connectionResult.GetType() != typeof(WebSocketReceiveResult))
                            throw new TimeoutException("Connection failed.");

                        if (connectionResult.MessageType == WebSocketMessageType.Close)
                        {
                            await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server closed", CancellationToken.None);
                            return;
                        }

                        var ack = Encoding.UTF8.GetString(buffer, 0, connectionResult.Count);
                        var ackMessage = converter.DeserializeData<ConnectionAckMessage>(ack);

                        if (ackMessage?.Type != MessageType.connection_ack)
                            throw new Exception("No se recibió el connection_ack del servidor.");

                        if (LoggingEnabled)
                            logger?.LogInformation("Confirmación recibida, iniciando loop de respuesta...");

                        _websocketCts = new CancellationTokenSource();
                        _pingLoopCts = new CancellationTokenSource();
                        _receiveLoopCts = new CancellationTokenSource();

                        if(options.AutoReconnect)
                            _timeoutTask ??= ReconnectLoop();

                        _processingTask ??= Parallel.ForEachAsync(Enumerable.Range(0, options.MessageProcessorsCount), async (x, y) =>
                        {
                            await HandleIncomingMessage();
                        });

                        _pingTask = PingMessageLoop(_pingLoopCts.Token);

                        _receiveTask = ReceiveLoopAsync(_receiveLoopCts.Token);

                        if (options.WebsocketRequiresPong)
                        {
                            _heartbeatWatcher = new HeartbeatWatcher(options.WebsocketTimeout, async () =>
                            {
                                IsReady = false;

                                if (LoggingEnabled)
                                    logger?.LogInformation("Socket timed out.");
                            });
                        }


                        IsReady = true;

                        foreach (var kvp in _subscriptions.Values)
                        {
                            if (kvp.ShouldReconnect)
                            {
                                var request = converter.DeserializeJsonElement<SubscriptionRequest>(kvp.RequestData!.Request);
                                await SendMessageAsync(request!);
                            }
                            else
                            {
                                kvp.OnError(new HubconRemoteException("Websocket connection lost. The subscription was not configured for reconnection."));
                            }
                        }

                        foreach (var kvp in _streams.Values)
                        {
                            if (kvp.Item1.ShouldReconnect)
                            {
                                var request = converter.DeserializeJsonElement<SubscriptionRequest>(kvp.Item1.RequestData!.Request);
                                await SendMessageAsync(request!);
                            }
                            else
                            {
                                kvp.Item1.OnError(new HubconRemoteException("Websocket connection lost. The subscription was not configured for reconnection."));
                            }
                        }

                        return;
                    }
                    catch (Exception ex)
                    {
                        _errorStream.OnNext(ex);
                        if (LoggingEnabled)
                            logger?.LogError(ex.Message);

                        int delay = Math.Min(1 * ++attempt, 30);

                        if (LoggingEnabled)
                            logger?.LogInformation($"Reconectando en {delay} segundos...");

                        await Task.Delay(delay * 1000, _cts.Token);

                        foreach (var item in _ingests)
                        {
                            _ingests.TryRemove(item);
                            item.Value.Item1.TrySetCanceled();
                            await item.Value.Item2.CancelAsync();
                        }

                        foreach (var item in _ingestAck)
                        {
                            _ingestAck.TryRemove(item);
                            item.Value.TrySetCanceled();
                        }

                        foreach (var item in _ingestDataAck)
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

        private ValueTask SendMessageAsync<T>(T message, CancellationToken? cancellationToken = null)
        {
            var msg = converter.Serialize(message);
            var buffer = Encoding.UTF8.GetBytes(msg);
            return _sendChannel.Writer.WriteAsync(buffer, cancellationToken ?? _cts.Token);
        }

        private async Task PingMessageLoop(CancellationToken cancellationToken)
        {
            if(options.WebsocketPingInterval <= TimeSpan.Zero)
            {
                if (LoggingEnabled)
                    logger?.LogInformation("PingMessageLoop no iniciado porque WebsocketPingInterval es menor o igual a cero.");

                return;
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (_webSocket?.State == WebSocketState.Open && IsReady == true)
                        await SendMessageAsync(new PingMessage(Guid.NewGuid()));

                    await Task.Delay(options.WebsocketPingInterval, cancellationToken);
                }
                catch (Exception ex)
                {
                    if (LoggingEnabled)
                        logger?.LogError($"Error en PingMessageLoop: {ex.Message}");
                }
            }
        }

        int cantidad = 0;
        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            if (LoggingEnabled)
                Interlocked.Increment(ref cantidad);

            if (LoggingEnabled)
                logger?.LogInformation($"ReceiveLoop iniciado. Cantidad: {Volatile.Read(ref cantidad)}");

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        if (_webSocket == null) break;

                        var parts = new List<IMemoryOwner<byte>>();
                        int totalBytes = 0;

                        ValueWebSocketReceiveResult result;

                        do
                        {
                            var part = MemoryPool<byte>.Shared.Rent(4096);
                            var segment = part.Memory;

                            result = await _webSocket.ReceiveAsync(segment, cancellationToken);
                            cancellationToken.ThrowIfCancellationRequested();

                            if (result.MessageType == WebSocketMessageType.Close)
                            {
                                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server closed", CancellationToken.None);
                                throw new OperationCanceledException();
                            }

                            if (result.Count < segment.Length)
                                part = new TrimmedMemoryOwner(part, result.Count); // Recorta a lo usado

                            totalBytes += result.Count;
                            parts.Add(part);
                        }
                        while (!result.EndOfMessage);

                        // Concatenamos todos los fragmentos a un solo buffer
                        var finalOwner = MemoryPool<byte>.Shared.Rent(totalBytes);
                        var finalMemory = finalOwner.Memory.Slice(0, totalBytes);
                        int offset = 0;

                        foreach (var part in parts)
                        {
                            part.Memory.Slice(0).CopyTo(finalMemory.Slice(offset));
                            offset += part.Memory.Length;
                            part.Dispose(); // Liberamos cada fragmento individual
                        }

                        await _messageChannel.Writer.WriteAsync(new TrimmedMemoryOwner(finalOwner, totalBytes), cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        if (LoggingEnabled)
                            logger?.LogError(ex.ToString());

                        break;
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (LoggingEnabled)
                    logger?.LogError("Error en ReceiveLoop: " + ex.Message);

                _errorStream.OnNext(ex);
            }
            finally
            {
                if (LoggingEnabled)
                {
                    logger?.LogInformation($"ReceiveLoop terminado. Cantidad: {Volatile.Read(ref cantidad)}");
                    Interlocked.Decrement(ref cantidad);
                }
            }
        }

        private async Task SendLoopAsync(ClientWebSocket _webSocket, CancellationToken cancellationToken)
        {
            try
            {
                while (await _sendChannel.Reader.WaitToReadAsync(cancellationToken))
                {
                    try
                    {
                        while (_sendChannel.Reader.TryRead(out var buffer))
                        {
                            if (_webSocket?.State != WebSocketState.Open)
                                await EnsureConnectedAsync();

                            var segment = new ArraySegment<byte>(buffer);
                            await _webSocket!.SendAsync(segment, WebSocketMessageType.Text, true, cancellationToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (LoggingEnabled)
                            logger?.LogError($"Error en SendLoopAsync: {ex.Message}");

                        _errorStream.OnNext(ex);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Cancelación esperada, no hacer nada.
            }
            catch (Exception ex)
            {

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
                        if (LoggingEnabled)
                            logger?.LogInformation("WebSocket no está abierto. Reconectando...");

                        await EnsureConnectedAsync();
                    }
                }
                catch (Exception ex)
                {
                    if (LoggingEnabled)
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
                        await SendMessageAsync(new SubscriptionCompleteMessage(request.Id));
                        break;
                }
            }
            catch (Exception ex)
            {
                if (LoggingEnabled)
                    logger?.LogError(ex, ex.Message);

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
            finally { /*Ignore*/ }
        }
    }
}