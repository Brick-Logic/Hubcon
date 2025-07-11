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
using K4os.Compression.LZ4;
using K4os.Compression.LZ4.Internal;
using K4os.Compression.LZ4.Streams;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Reactive;
using System.Reactive.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using System.Threading;
using System.Threading.Channels;
using System.Xml.Linq;

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
        private CancellationTokenSource? _sendLoopCts;
        private Task? _receiveTask;
        private bool _disposed = false;

        private bool IsReady = false;

        private HeartbeatWatcher? _heartbeatWatcher;

        private readonly GenericObservable<PongMessage> _pongStream;
        private readonly GenericObservable<Exception> _errorStream;

        public IObservable<PongMessage> PongStream => _pongStream;
        public IObservable<Exception> ErrorStream => _errorStream;

        private Task? _pingTask;
        private Task? _sendTask;
        private Task? _timeoutTask;
        private Task? _processingTask;

        private string _lastPongId = Guid.Empty.ToString();
        private DateTime _lastPongTime = DateTime.UtcNow;
        private readonly TimeSpan _timeoutSeconds = TimeSpan.FromSeconds(5);

        private GenericObservable<string> _incomingMessageStream;
        private AsyncObserver<string> _incomingMessageObserver;
        public HubconWebSocketClient(Uri uri, IDynamicConverter converter, ILogger<HubconWebSocketClient>? logger = null)
        {
            _pongStream = new GenericObservable<PongMessage>(converter);
            _errorStream = new GenericObservable<Exception>(converter);
            _uri = uri;
            this.converter = converter;
            this.logger = logger;
            //_messageChannel = Channel.CreateUnbounded<string>();
            _incomingMessageStream = new GenericObservable<string>(converter);
            _incomingMessageObserver = new AsyncObserver<string>();
            _incomingMessageStream.Subscribe(_incomingMessageObserver);
        }

        public async Task<IObservable<T>> Subscribe<T>(object payload)
        {
            var request = new SubscriptionInitMessage(Guid.NewGuid().ToString(), converter.SerializeToElement(payload));
            var observable = new GenericObservable<T>(this, request.Id, converter.SerializeToElement(request), RequestType.Subscription, converter);
            if (!_subscriptions.TryAdd(request.Id, observable))
                throw new InvalidOperationException($"Ya existe una suscripción con Id {request.Id}");

            await SendMessageAsync(request);
            return observable;
        }

        public async Task<IObservable<T>> Stream<T>(object payload)
        {
            var request = new StreamInitMessage(Guid.NewGuid().ToString(), converter.SerializeToElement(payload));
            var observable = new GenericObservable<T>(this, request.Id, converter.SerializeToElement(request), RequestType.Subscription, converter);

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

            await SendMessageAsync(request);
            return observable;
        }

        public async Task IngestMultiple(IOperationRequest payload, bool needsAck = false)
        {
            using var cts = new CancellationTokenSource();
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
                foreach (var kvp in payload.Arguments!)
                {
                    if (kvp.Value != null && EnumerableTools.IsAsyncEnumerable(kvp.Value))
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

                            var message = new IngestDataMessage(source.Key, converter.SerializeToElement(item));
                            await Task.WhenAny(SendMessageAsync(message, cts.Token), generalTcs.Task);

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

                await SendMessageAsync(ingestRequest);

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
                    await SendMessageAsync(new IngestCompleteMessage(initialAckId, sources.Keys.ToArray()));
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
            public static IAsyncEnumerable<JsonElement> WrapAsJsonElementEnumerable(object value, Type elementType, IDynamicConverter converter)
            {
                if (value == null)
                    throw new ArgumentNullException(nameof(value));

                if (!typeof(IAsyncEnumerable<>).MakeGenericType(elementType).IsAssignableFrom(value.GetType()))
                    throw new InvalidCastException($"Expected IAsyncEnumerable<{elementType.Name}> but got {value.GetType().Name}");

                var method = typeof(IngestUtils)
                    .GetMethod(nameof(WrapGeneric), BindingFlags.NonPublic | BindingFlags.Static)!
                    .MakeGenericMethod(elementType);

                return (IAsyncEnumerable<JsonElement>)method.Invoke(null, new object[] { value, converter })!;
            }

            private static async IAsyncEnumerable<JsonElement> WrapGeneric<T>(IAsyncEnumerable<T> source, IDynamicConverter converter)
            {
                await foreach (var item in source)
                {
                    var json = converter.SerializeToElement(item);
                    yield return json;
                }
            }
        }

        public async Task SendAsync(object payload)
        {
            OperationCallMessage? request = new OperationCallMessage(Guid.NewGuid().ToString("N"), converter.SerializeToElement(payload));
            //TaskCompletionSource<OperationResponseMessage>? tcs = new TaskCompletionSource<OperationResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            //_operationTcs.TryAdd(Id, tcs);
            //OperationResponseMessage? response = null;
            //bool? result = false;

            try
            {
                await SendMessageAsync(request);
                //response = await WaitWithTimeoutAsync(TimeSpan.FromSeconds(5), tcs.Task);
                //result = response != null;
            }
            finally
            {
                //_operationTcs.TryRemove(Id, out _);
            }

            return;

            //if (result != null && result == true)
            //    return;
            //else
            //    throw new Exception("Ocurrió un error mientras se ejecutaba la operación.");
        }

        public async Task<TOut> InvokeAsync<Tin, TOut>(Tin payload)
        {
            OperationInvokeMessage? request = new OperationInvokeMessage(Guid.NewGuid().ToString("N"), converter.SerializeToElement(payload));
            //TaskCompletionSource<OperationResponseMessage>? tcs = new TaskCompletionSource<OperationResponseMessage>();
            //_operationTcs.TryAdd(request.Id, tcs);
            //OperationResponseMessage? response = null;

            try
            {
                await SendMessageAsync(request);

                //response = await WaitWithTimeoutAsync(TimeSpan.FromSeconds(5), tcs.Task);
            }
            finally
            {
                //_operationTcs.TryRemove(request.Id, out _);
            }
            return default!;

            //return response == null
            //    ? throw new TimeoutException("The request timed out.")
            //    : converter.DeserializeJsonElement<TOut>(response.Result)!;
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

        private async Task<T> WaitWithTimeoutAsync<T>(TimeSpan timeout, Task<T> task)
        {
            if (timeout == TimeSpan.Zero)
            {
                return await task;
            }

            using var cts = new CancellationTokenSource();

            var delayTask = Task.Delay(timeout, cts.Token);

            var completedTask = await Task.WhenAny(task, delayTask);

            if (completedTask == task)
            {
                cts.Cancel();
                return await task;
            }
            else
            {
                return default!;
            }
        }

        private async Task HandleIncomingMessage()
        {
            try
            {
                await foreach(var item in _incomingMessageObserver.GetAsyncEnumerable(_cts.Token))
                {
                    try
                    {                       
                        if (!TryExtractTypeFromMemory(item!, out var type, out var jsonDocument))
                        {
                            logger?.LogError("Mensaje inválido recibido o sin tipo.");
                            continue;
                        }

                        using (jsonDocument)
                        {
                            var root = jsonDocument.RootElement.Clone();

                            switch (type)
                            {
                                case "pong":
                                    var pong = converter.DeserializeJsonElement<PongMessage>(root)!;

                                    if (_lastPongId == pong.Id)
                                    {
                                        await _webSocket!.CloseAsync(WebSocketCloseStatus.InvalidPayloadData, "Pong error", default);
                                        return;
                                    }
                                    _lastPongId = pong.Id;
                                    _lastPongTime = DateTime.UtcNow;
                                    _heartbeatWatcher?.NotifyHeartbeat();
                                    _pongStream.OnNext(pong);
                                    break;

                                case "subscription_data":
                                    var subData = converter.DeserializeJsonElement<SubscriptionDataMessage>(root);
                                    if (subData?.Id != null && _subscriptions.TryGetValue(subData.Id, out var sub))
                                        sub.OnNextElement(subData.Data);
                                    break;

                                case "stream_data":
                                    var streamData = converter.DeserializeJsonElement<StreamDataMessage>(root);
                                    if (streamData?.Id != null && _streams.TryGetValue(streamData.Id, out var stream))
                                    {
                                        stream.Item1.OnNextElement(streamData.Data);
                                        stream.Item3.NotifyHeartbeat();
                                    }
                                    break;

                                case "stream_complete":
                                    var streamComplete = converter.DeserializeJsonElement<StreamCompleteMessage>(root);
                                    if (streamComplete?.Id != null && _streams.TryGetValue(streamComplete.Id, out var streamInfo))
                                        await streamInfo.Item2.CancelAsync();
                                    break;

                                case "error":
                                    var error = converter.DeserializeJsonElement<ErrorMessage>(root);
                                    if (error?.Id != null && _subscriptions.TryGetValue(error.Id, out var subErr))
                                        subErr.OnError(new Exception(error.Error));
                                    break;

                                case "ingest_init_ack":
                                    var initAck = converter.DeserializeJsonElement<IngestInitAckMessage>(root);
                                    if (initAck != null && _ingestAck.TryGetValue(initAck.Id, out var tcsInit))
                                        tcsInit.TrySetResult(initAck);
                                    break;

                                case "ingest_data_ack":
                                    var dataAck = converter.DeserializeJsonElement<IngestDataAckMessage>(root);
                                    if (dataAck != null && _ingestDataAck.TryGetValue(dataAck.Id, out var tcsData))
                                        tcsData.TrySetResult(dataAck);
                                    break;

                                case "operation_response":
                                    var opResp = converter.DeserializeJsonElement<OperationResponseMessage>(root);
                                    if (opResp != null && _operationTcs.TryGetValue(opResp.Id, out var tcsOp))
                                        tcsOp.TrySetResult(opResp);
                                    break;

                                default:
                                    var msg = $"Tipo de mensaje no soportado: {type}";
                                    _errorStream.OnNext(new NotSupportedException(msg));
                                    logger?.LogError(msg);
                                    break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError($"Error en HandleIncomingMessage: {ex}");
                        _errorStream.OnNext(ex);
                    }
                }
            }
            finally
            {
                logger?.LogInformation("HandleIncomingMessage terminado.");
            }
        }

        // Extractor de tipo optimizado para ReadOnlyMemory<byte>
        private bool TryExtractTypeFromMemory(string value, out string type, out JsonDocument jsonDocument)
        {
            type = string.Empty;
            jsonDocument = null!;

            try
            {
                // Parsear directamente desde ReadOnlyMemory<byte>
                jsonDocument = JsonDocument.Parse(value);
                var root = jsonDocument.RootElement.Clone();

                if (root.TryGetProperty("type", out var typeProperty))
                {
                    type = typeProperty.GetString() ?? string.Empty;
                    return !string.IsNullOrEmpty(type);
                }

                return false;
            }
            catch (JsonException)
            {
                jsonDocument?.Dispose();
                return false;
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
                    catch (Exception ex) { }
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

                        if (_heartbeatWatcher != null)
                        {
                            await _heartbeatWatcher.DisposeAsync();
                            _heartbeatWatcher = null;
                        }

                        logger?.LogInformation("Intentando conectar...");
                        WebSocketOptions?.Invoke(_webSocket.Options);

                        _sendLoopCts = new CancellationTokenSource();
                        _sendTask = Task.Run(() => ProcessSendQueueAsync(_sendLoopCts.Token));

                        var uriBuilder = new UriBuilder(_uri);

                        var token = AuthorizationTokenProvider?.Invoke();

                        if (!string.IsNullOrEmpty(token))
                            uriBuilder.AddQueryParameter("access_token", token);

                        await _webSocket.ConnectAsync(uriBuilder.Uri, _cts.Token);

                        logger?.LogInformation("Conectado, intentando handshake...");
                        await QueueMessageAsync(new ConnectionInitMessage());

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
                        var ackMessage = converter.DeserializeData<ConnectionAckMessage>(ack);

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

                        IsReady = true;

                        foreach (var kvp in _subscriptions.Values)
                        {
                            var request = converter.DeserializeJsonElement<SubscriptionInitMessage>(kvp.RequestData!.Request);
                            await SendMessageAsync(request!);
                        }

                        return;
                    }
                    catch (Exception ex)
                    {
                        _errorStream.OnNext(ex);
                        logger?.LogError(ex.Message);
                        int delay = Math.Min(1 * ++attempt, 10);
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

        // Pre-configurar JsonWriterOptions para evitar allocaciones
        private static readonly JsonWriterOptions _jsonOptions = new() { Indented = false };

        private async Task SendMessageAsync<T>(T value, CancellationToken? cancellationToken = null)
        {
            if (_webSocket?.State != WebSocketState.Open)
                await EnsureConnectedAsync();

            await QueueMessageAsync(value);
        }

        // Para casos donde necesites envío batch (recomendado para 50k RPS)
        private readonly Channel<QueuedMessage> _sendQueue = Channel.CreateUnbounded<QueuedMessage>();

        private record QueuedMessage(object Value, CancellationToken Token);

        // Procesador batch en background
        private async Task ProcessSendQueueAsync(CancellationToken cancellationToken = default)
        {
            await foreach (var message in _sendQueue.Reader.ReadAllAsync(_cts.Token))
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                if (_webSocket?.State != WebSocketState.Open)
                    await EnsureConnectedAsync();

                try
                {
                    await SendMessageCoreAsync(message.Value, cancellationToken);
                }
                finally
                {

                }
            }
        }

        private async Task SendMessageCoreAsync<T>(T value, CancellationToken cancellationToken = default)
        {
            var serialized = converter.Serialize(value);

            await _webSocket!.SendAsync(
                Encoding.UTF8.GetBytes(serialized),
                WebSocketMessageType.Text,
                true,
                cancellationToken
            );
        }

        // Método público para encolar mensajes
        public async Task QueueMessageAsync<T>(T value, CancellationToken cancellationToken = default)
        {
            try
            {
                await _sendLock.WaitAsync();
                var queuedMessage = new QueuedMessage(value, cancellationToken);
                //_sendQueue.Writer.TryWrite(queuedMessage);
                await SendMessageCoreAsync(value);
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
                        await SendMessageAsync(new PingMessage(Guid.NewGuid().ToString()));

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

                        _incomingMessageStream.OnNext(sb.ToString());
                        //_messageQueue.Enqueue(sb.ToString());
                        //await _messageChannel.Writer.WriteAsync(sb.ToString(), cancellationToken);
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
                        await SendMessageAsync(new SubscriptionCompleteMessage(request.Id));
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
            _cts?.Dispose();

            try
            {
                _webSocket?.Abort();
                _webSocket?.Dispose();
                _receiveLoopCts?.Cancel();
                _receiveLoopCts?.Dispose();
                _sendQueue.Writer.Complete();
                _sendLock?.Dispose();

                await Task.WhenAll(_pingTask ?? Task.CompletedTask, _timeoutTask ?? Task.CompletedTask);
            }
            finally { /*Ignore*/ }
        }
    }
}