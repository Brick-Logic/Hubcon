using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Client.Abstractions.Models;
using Hubcon.Client.Core.Exceptions;
using Hubcon.Client.Core.Extensions;
using Hubcon.Client.Core.Helpers;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Core.Tools;
using Hubcon.Shared.Core.Websockets;
using Hubcon.Shared.Core.Websockets.Events;
using Hubcon.Shared.Core.Websockets.Heartbeat;
using Hubcon.Shared.Core.Websockets.Interfaces;
using Hubcon.Shared.Core.Websockets.Messages.Cancellation;
using Hubcon.Shared.Core.Websockets.Messages.Connection;
using Hubcon.Shared.Core.Websockets.Messages.Generic;
using Hubcon.Shared.Core.Websockets.Messages.Ingest;
using Hubcon.Shared.Core.Websockets.Messages.Operation;
using Hubcon.Shared.Core.Websockets.Messages.Ping;
using Hubcon.Shared.Core.Websockets.Messages.Streams;
using Hubcon.Shared.Core.Websockets.Messages.Token;
using Hubcon.Shared.Core.Websockets.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Net.WebSockets;
using System.Reactive.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using System.Timers;

#pragma warning disable CS1591

namespace Hubcon.Client.Core.Websockets
{
    public sealed class HubconWebSocketClient : IAsyncDisposable
    {
        private readonly TransportContext context;
        private readonly IDynamicConverter converter;
        private readonly IClientOptions options;
        private readonly IServiceProvider serviceProvider;
        private readonly ILogger<HubconWebSocketClient>? logger;
        private ClientWebSocket? _webSocket;

        public bool CloseSent = false;

        public bool LoggingEnabled { get; set; } = true;
        private Uri _uri;
        public void SetUri(Uri uri) => _uri = uri;

        public Action<ClientWebSocketOptions, IServiceProvider>? WebSocketOptions { get; set; }
        public Func<string?>? AuthorizationTokenProvider { get; set; }

        private readonly ConcurrentDictionary<Guid, (BaseObservable, CancellationTokenSource, HeartbeatWatcher, CancellationTokenRegistration)> _streams
            = new ConcurrentDictionary<Guid, (BaseObservable, CancellationTokenSource, HeartbeatWatcher, CancellationTokenRegistration)>();

        private readonly ConcurrentDictionary<Guid, (TaskCompletionSource<IngestResultMessage>, CancellationTokenSource, CancellationTokenRegistration)> _ingests
            = new ConcurrentDictionary<Guid, (TaskCompletionSource<IngestResultMessage>, CancellationTokenSource, CancellationTokenRegistration)>();

        private readonly ConcurrentDictionary<Guid, TaskCompletionSource<BaseMessage>> _requestsCts
            = new ConcurrentDictionary<Guid, TaskCompletionSource<BaseMessage>>();

        private readonly SemaphoreSlim _reconnectLock = new SemaphoreSlim(1, 1);

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private CancellationTokenSource? _websocketCts = new CancellationTokenSource();
        private CancellationTokenSource? _receiveLoopCts;
        private CancellationTokenSource? _sendLoopCts;
        private string connectionId = "";

        private bool _disposed = false;

        private bool IsReady = false;

        public bool IsConnected => IsReady && _webSocket?.State == WebSocketState.Open;

        private HeartbeatWatcher? _heartbeatWatcher;

        private readonly GenericObservable<PongMessage> _pongStream;
        private readonly GenericObservable<Exception> _errorStream;

        public IObservable<PongMessage> PongStream => _pongStream;
        public IObservable<Exception> ErrorStream => _errorStream;

        public IClientOptions ClientOptions { get; set; }

        private System.Timers.Timer? _pingTimer;
        private System.Timers.Timer? _timeoutTimer;
        private List<Task?> _processingTasks;
        private Task? _receiveTask;
        private Task? _sendTask;

        private Guid _lastPongId = Guid.Empty;
        private DateTime _lastPongTime = DateTime.UtcNow;

        private readonly Channel<TrimmedMemoryOwner> _messageChannel;
        private readonly Channel<ByteMessage> _sendChannel;

        public HubconWebSocketClient(Uri uri, TransportContext context, ILogger<HubconWebSocketClient>? logger = null)
        {
            this.context = context;
            _pongStream = new GenericObservable<PongMessage>(context.Converter);
            _errorStream = new GenericObservable<Exception>(context.Converter);
            _uri = uri;
            this.logger = logger;
            options = context.ClientOptions;
            converter = context.Converter;
            ClientOptions = context.ClientOptions;
            serviceProvider = context.ProxyServiceProvider;

            _messageChannel = Channel.CreateBounded<TrimmedMemoryOwner>(
                new BoundedChannelOptions(20000 * options.MessageProcessorsCount)
                {
                    FullMode = BoundedChannelFullMode.Wait,
                    SingleWriter = true,
                    SingleReader = false
                });

            _sendChannel = Channel.CreateBounded<ByteMessage>(new BoundedChannelOptions(20000)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleWriter = false,
                SingleReader = true
            });
        }

        public async Task<IObservable<T>> Stream<T>(IOperationRequest payload, bool remoteCancelEnabled, CancellationToken cancellationToken = default)
        {
            if (_webSocket?.State != WebSocketState.Open)
                await EnsureConnectedAsync();

            using var request = new StreamInitMessage(Guid.NewGuid(), connectionId, converter.SerializeToElement(payload));
            var tcs = new CancellationTokenSource();

            HeartbeatWatcher hw = null!;

            CancellationTokenRegistration registration = cancellationToken.Register(async () =>
            {
                if (remoteCancelEnabled)
                    await SendMessageAsync(new CancelMessage(request.Id, connectionId));

                tcs.Cancel();
            });

            hw = new HeartbeatWatcher(TimeSpan.Zero, async () =>
            {
                if (_streams.TryRemove(request.Id, out var obs))
                {
                    obs.Item1.OnCompleted();
                    if (!obs.Item2.IsCancellationRequested)
                    {
                        obs.Item2.Cancel();
                        obs.Item2.Dispose();
                    }
                    obs.Item4.Dispose();
                }
            });

            var observable = new GenericObservable<T>(
                null!,
                request.Id,
                converter.SerializeToElement(request),
                RequestType.Stream,
                converter,
                async () => await hw.DisposeAsync(),
                options.ReconnectStreams);

            if (!_streams.TryAdd(request.Id, (observable, tcs, hw, registration)))
                throw new InvalidOperationException($"Ya existe un stream con Id {request.Id}");

            await SendMessageAsync(request);

            return observable;
        }

        public async Task<BaseMessage?> SendAndReceive<TRequest>(TRequest message, CancellationToken cancellationToken = default) where TRequest : BaseMessage
        {
            try
            {
                var responseTcs = new TaskCompletionSource<BaseMessage>();

                if (_requestsCts.TryAdd(message.Id, responseTcs))
                {
                    await SendMessageAsync(message, cancellationToken);
                    return await TimeoutHelper.WaitWithTimeoutAsync(responseTcs.Task.WaitAsync, options.WebsocketTimeout);
                }
            }
            finally
            {
                _requestsCts.TryRemove(message.Id, out _);
            }

            throw new HubconGenericException("Cannot send the same message multiple times.");
        }

        public async Task<BaseMessage?> Receive(Guid id, CancellationToken cancellationToken = default)
        {
            try
            {
                var responseTcs = _requestsCts.GetOrAdd(id, _ => new TaskCompletionSource<BaseMessage>());
                return await TimeoutHelper.WaitWithTimeoutAsync(responseTcs.Task.WaitAsync, options.WebsocketTimeout);       
            }
            finally
            {
                _requestsCts.TryRemove(id, out _);
            }
        }

        public async Task<BaseMessage?> Receive(Guid id, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            try
            {
                var responseTcs = _requestsCts.GetOrAdd(id, _ => new TaskCompletionSource<BaseMessage>());
                return await TimeoutHelper.WaitWithTimeoutAsync(responseTcs.Task.WaitAsync, timeout);
            }
            finally
            {
                _requestsCts.TryRemove(id, out _);
            }
        }

        public async Task<T> IngestMultiple<T>(
            IOperationRequest operationRequest,
            bool remoteCancelEnabled,
            IClientOptions? clientOptions = null,
            IOperationOptions? operationOptions = null,
            CancellationToken cancellationToken = default)
        {
            using var cts = new CancellationTokenSource();
            var sourceTasks = new List<Task>();
            var initAckTcs = new TaskCompletionSource<bool>();
            var generalTcs = new TaskCompletionSource<IngestResultMessage>();
            var sources = new ConcurrentDictionary<Guid, IAsyncEnumerable<JsonElement>>();
            var initialAckId = Guid.NewGuid();

            using var registration = cancellationToken.Register(async () =>
            {
                if (remoteCancelEnabled)
                {
                    await SendMessageAsync(new CancelMessage(initialAckId, connectionId));
                    cts.Cancel();
                    generalTcs.TrySetException(new OperationCanceledException());
                }
            });

            _ingests.TryAdd(initialAckId, (generalTcs, cts, registration));

            if (_webSocket?.State != WebSocketState.Open)
                await EnsureConnectedAsync();

            try
            {
                var dict = operationRequest.Arguments.ToDictionary(kvp => kvp.Key, kvp => (object?)kvp.Value);
                foreach (var kvp in operationRequest.Arguments)
                {
                    if (kvp.Value != null && EnumerableTools.IsAsyncEnumerable(kvp.Value))
                    {
                        var obj = kvp.Value;
                        var id = Guid.NewGuid();
                        dict[kvp.Key] = id;
                        var stream = EnumerableTools.Wrap(obj, cancellationToken);
                        sources.TryAdd(id, stream!);
                    }
                }

                operationRequest.AssignArguments(dict!);

                RateLimiter? sharedLimiter = null;
                bool? useShared = null;

                if (operationOptions != null && operationOptions.RateBucketOptions != null)
                {
                    if (operationOptions.RateLimiterIsShared)
                    {
                        sharedLimiter = new TokenBucketRateLimiter(operationOptions.RateBucketOptions);
                        useShared = true;
                    }
                    else
                    {
                        useShared = false;
                    }
                }

                foreach (var source in sources)
                {
                    var sourceTask = Task.Factory.StartNew(async () =>
                    {
                        try
                        {
                            var shouldIngest = await initAckTcs.Task;

                            if (!shouldIngest)
                                return;

                            RateLimiter? limiter = sharedLimiter ?? (useShared == false
                                ? new TokenBucketRateLimiter(operationOptions!.RateBucketOptions!)
                                : null);

                            await foreach (var item in source.Value.WithCancellation(cancellationToken))
                            {
                                if (generalTcs.Task.IsCompleted || cancellationToken.IsCancellationRequested)
                                    break;

                                var message = new IngestDataMessage(source.Key, connectionId, item);

                                try
                                {
                                    await RateLimiterHelper.AcquireAsync(clientOptions, clientOptions?.RateBucket, clientOptions?.IngestRateBucket, limiter);
                                    await SendMessageAsync(message, cancellationToken);
                                }
                                catch (Exception ex)
                                {
                                    if (LoggingEnabled)
                                        logger?.LogError(ex, $"Error al enviar dato en ingest stream {source.Key}");

                                    _errorStream.OnNext(ex);
                                }

                                if (generalTcs.Task.IsCompleted || cancellationToken.IsCancellationRequested)
                                    break;
                            }
                        }
                        catch (Exception ex)
                        {
                            if (LoggingEnabled)
                                logger?.LogError(ex, $"Error en ingest stream {source.Key}");

                            _errorStream.OnNext(ex);
                        }
                    },
                    cancellationToken,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default).Unwrap();

                    sourceTasks.Add(sourceTask);
                }

                await Task.Delay(100);

                var ingestRequest = new IngestInitMessage(initialAckId, connectionId, sources.Keys.ToArray(), converter.SerializeToElement(operationRequest), default);

                try
                {
                    var ack = await SendAndReceive(ingestRequest, cancellationToken);

                    if (ack?.Error != null)
                    {
                        initAckTcs.TrySetResult(false);
                        return converter.DeserializeData<T>(ack.Error);
                    }

                    initAckTcs.TrySetResult(true);
                }
                catch (Exception ex)
                {
                    if (LoggingEnabled)
                        logger?.LogError(ex, "Error al enviar IngestInitMessage");

                    _errorStream.OnNext(ex);
                }

                var receiver = Receive(initialAckId, TimeSpan.FromDays(23), cancellationToken);

                try
                {
                    var allIngests = Task.WhenAll(sourceTasks);
                    var whenany = Task.WhenAny(allIngests, receiver);
                    await whenany;
                }
                finally
                {
                    registration.Dispose();
                }

                await SendMessageAsync(new IngestCompleteMessage(initialAckId, connectionId, sources.Keys.ToArray()), cancellationToken);

                using BaseMessage? result = await receiver;

                if (result == null) 
                    throw new HubconRemoteException("Received an empty response.");

                if (result.Error != null)
                    return converter.DeserializeData<T>(result.Error);

                var response = converter.DeserializeJsonElement<T>(new IngestResultMessage(result).Data) ?? throw new HubconRemoteException("Received an empty response.");

                return response;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (LoggingEnabled)
                    logger?.LogError(ex, "Error general en IngestMultiple");

                _errorStream.OnNext(ex);

                if (HubconContext.Current.IsWrapped)
                    return default!;

                throw new HubconGenericException(ex.Message, ex);
            }
            finally
            {
                if (IsReady && !cts.IsCancellationRequested)
                {
                    var msg = new IngestCompleteMessage(initialAckId, connectionId, sources.Keys.ToArray());
                    await SendMessageAsync(msg);
                }

                _ingests.TryRemove(initialAckId, out var removedIngest);
                removedIngest.Item1?.TrySetCanceled();
                removedIngest.Item2?.Cancel();
            }
        }

        public async Task SendAsync(IOperationRequest payload, bool remoteCancelEnabled, CancellationToken cancellationToken = default)
        {
            if (_webSocket?.State != WebSocketState.Open)
                await EnsureConnectedAsync();

            var request = new OperationCallMessage(Guid.NewGuid(), connectionId, converter.SerializeToElement(payload));

            using var registration = cancellationToken.Register(async () =>
            {
                if (remoteCancelEnabled)
                    await SendMessageAsync(new CancelMessage(request.Id, connectionId));
            });

            await SendMessageAsync(request, cancellationToken);
        }

        public async Task<T> InvokeAsync<T>(IOperationRequest payload, bool remoteCancelEnabled, bool responseIsWrapped, CancellationToken cancellationToken = default)
        {
            if (_webSocket?.State != WebSocketState.Open)
                await EnsureConnectedAsync();

            var request = new OperationInvokeMessage(Guid.NewGuid(), connectionId, converter.SerializeToElement(payload));

            try
            {
                using var registration = cancellationToken.Register(async () =>
                {
                    if (remoteCancelEnabled)
                        await SendMessageAsync(new CancelMessage(request.Id, connectionId));
                });

                using var response = await SendAndReceive(request, cancellationToken);

                if (response == null)
                    throw new HubconGenericException("There was an unknown error or the request timed out.");

                if (response.Error != null)
                    return converter.DeserializeData<T>(response.Error);

                using var converted = new OperationResponseMessage(response);
                return converter.DeserializeData<T>(converted.Result);
            }
            catch (Exception ex)
            {
                if (LoggingEnabled)
                    logger?.LogError(ex.Message);

                _errorStream.OnNext(ex);

                throw;
            }
        }

        private async Task HandleIncomingMessage()
        {
            TrimmedMemoryOwner tmo = null!;

            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    try
                    {
                        if (_webSocket?.State != WebSocketState.Open)
                            await EnsureConnectedAsync();

                        tmo = await _messageChannel.Reader.ReadAsync();

                        var message = new BaseMessage(tmo);

                        if (message.ConnectionId != connectionId)
                            continue;

                        switch (message.Type)
                        {
                            case MessageType.pong:
                                if (!options.WebsocketRequiresPong)
                                    break;

                                var pongMessage = new PongMessage(message);

                                if (_lastPongId == pongMessage.Id)
                                {
                                    _webSocket!.Abort();
                                    return;
                                }

                                _lastPongId = pongMessage.Id;
                                _lastPongTime = DateTime.UtcNow;
                                _heartbeatWatcher?.NotifyHeartbeat();
                                _pongStream.OnNext(pongMessage);

                                await context.InterceptorManager.CallInterceptor(InterceptorType.OnPong);
                                break;

                            case MessageType.error:
                                if (message?.Id != null && _requestsCts.TryGetValue(message.Id, out var subToError))
                                {
                                    subToError.TrySetResult(message);
                                }

                                break;

                            case MessageType.stream_data:
                                var streamData = new StreamDataMessage(message);
                                if (streamData?.Id != null && _streams.TryGetValue(streamData.Id, out var stream))
                                {
                                    stream.Item1.OnNextElement(streamData.Data);
                                    stream.Item3.NotifyHeartbeat();
                                }

                                break;

                            case MessageType.stream_complete:
                                var streamComplete = new StreamCompleteMessage(message);

                                if (streamComplete?.Id != null &&
                                    _streams.TryGetValue(streamComplete.Id, out var streamCompleteInfo))
                                {
                                    streamCompleteInfo.Item1.OnCompleted();
                                }

                                break;

                            case MessageType.ingest_init_ack:
                                if (_requestsCts.TryGetValue(message.Id, out var ingestInitAckTcs))
                                {
                                    ingestInitAckTcs.TrySetResult(message);
                                }

                                break;

                            case MessageType.ingest_result:
                                if (_requestsCts.TryGetValue(message.Id, out var ingestResultMessageTcs))
                                {
                                    ingestResultMessageTcs.TrySetResult(message);
                                }

                                break;

                            case MessageType.token_update:
                                if (_requestsCts.TryGetValue(message.Id, out var tokenUpdateResponseTcs))
                                {
                                    tokenUpdateResponseTcs.TrySetResult(message);
                                }

                                break;

                            case MessageType.ingest_data_ack:
                                if (_requestsCts.TryGetValue(message.Id, out var ingestDataAckTcs))
                                {
                                    ingestDataAckTcs.TrySetResult(message);
                                }

                                break;

                            case MessageType.operation_response:
                                if (_requestsCts.TryGetValue(message.Id, out var ormTcs))
                                {
                                    ormTcs.TrySetResult(message);
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
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        if (LoggingEnabled)
                            logger?.LogError($"Error en HandleIncomingMessage: {ex.Message}");

                        _errorStream.OnNext(ex);
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

        public async Task EnsureConnectedAsync(Uri? newUrl = null)
        {
            await _reconnectLock.WaitAsync();

            try
            {
                if (_webSocket?.State is WebSocketState.Open || _webSocket?.State is WebSocketState.Connecting)
                    return;

                if (_webSocket?.State is WebSocketState.Closed
                    || _webSocket?.State is WebSocketState.CloseReceived
                    || _webSocket?.State is WebSocketState.CloseSent)
                {
                    await context.InterceptorManager.CallInterceptor(InterceptorType.OnReconnect, _cts.Token);
                }

                int attempt = 0;
                while (!_cts.IsCancellationRequested)
                {
                    try
                    {
                        IsReady = false;
                        CloseSent = false;
                        CancelAll();
                        connectionId = "";

                        _webSocket = new ClientWebSocket();
                        _sendLoopCts = new CancellationTokenSource();
                        _sendTask = Task.Factory.StartNew(
                                    async () => await SendLoopAsync(_webSocket, _sendLoopCts.Token),
                                    _sendLoopCts.Token,
                                    TaskCreationOptions.LongRunning,
                                    TaskScheduler.Default).Unwrap();

                        if (_heartbeatWatcher != null)
                        {
                            await _heartbeatWatcher.DisposeAsync();
                            _heartbeatWatcher = null;
                        }

                        var url = newUrl ?? _uri;
                        _uri = url;

                        if (LoggingEnabled)
                            logger?.LogInformation("Intentando conectar...");

                        ClientOptions.WebSocketOptions?.Invoke(_webSocket.Options, serviceProvider);

                        var uriBuilder = new UriBuilder(url);
                        var token = AuthorizationTokenProvider?.Invoke();

                        if (!string.IsNullOrEmpty(token))
                            uriBuilder.AddQueryParameter("access_token", token);

                        _websocketCts = new CancellationTokenSource();
                        await context.InterceptorManager.CallInterceptor(InterceptorType.OnConnecting, _cts.Token);
                        await _webSocket.ConnectAsync(uriBuilder.Uri, _websocketCts.Token);

                        if (LoggingEnabled)
                            logger?.LogInformation("Conectado, intentando handshake...");

                        var msgId = Guid.NewGuid();
                        await SendMessageAsync(new ConnectionInitMessage(msgId));

                        var buffer = new byte[4096];

                        var receiveTask = _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);

                        var connectionResult = await TimeoutHelper.WaitWithTimeoutAsync(receiveTask.WaitAsync, options.WebsocketTimeout);

                        if (connectionResult == null || connectionResult.GetType() != typeof(WebSocketReceiveResult))
                        {
                            _webSocket.Abort();
                            throw new TimeoutException("Connection failed.");
                        }

                        if (connectionResult.MessageType == WebSocketMessageType.Close)
                        {
                            _webSocket.Abort();
                            return;
                        }

                        var ack = Encoding.UTF8.GetString(buffer, 0, connectionResult.Count);
                        var ackMessage = converter.DeserializeData<ConnectionAckMessage>(ack);

                        if (ackMessage?.Type != MessageType.connection_ack)
                        {
                            _webSocket.Abort();
                            throw new Exception("No se recibió el connection_ack del servidor.");
                        }

                        if (ackMessage.Id != msgId)
                        {
                            _webSocket.Abort();
                            throw new Exception("Se recibió una confirmación, pero el ID no es correcto. Rechazando conexión...");
                        }

                        if (LoggingEnabled)
                            logger?.LogInformation("Confirmación recibida, iniciando loop de respuesta...");

                        connectionId = ackMessage.ConnectionId;

                        _receiveLoopCts = new CancellationTokenSource();

                        if (options.AutoReconnect && _timeoutTimer == null)
                        {
                            _timeoutTimer ??= new System.Timers.Timer(5000);
                            _timeoutTimer.Elapsed += ReconnectLoop;
                            _timeoutTimer.AutoReset = true;
                            _timeoutTimer.Enabled = true;
                            _timeoutTimer.Start();
                        }

                        if (_processingTasks == null)
                        {
                            _processingTasks = new List<Task?>();
                            foreach (var number in Enumerable.Range(0, options.MessageProcessorsCount))
                            {
                                _processingTasks.Add(Task.Factory.StartNew(
                                    async () => await HandleIncomingMessage(),
                                    _cts.Token,
                                    TaskCreationOptions.LongRunning,
                                    TaskScheduler.Default).Unwrap());
                            }
                        }

                        if (_pingTimer == null)
                        {
                            _pingTimer ??= new System.Timers.Timer();
                            _pingTimer.Elapsed += PingMessageLoop;
                            _pingTimer.Interval = options.WebsocketPingInterval.TotalMilliseconds;
                            _pingTimer.Enabled = true;
                            _pingTimer.AutoReset = true;
                            _pingTimer.Start();
                        }

                        _receiveTask = Task.Factory.StartNew(
                                    async () => await ReceiveLoopAsync(_receiveLoopCts.Token),
                                    _receiveLoopCts.Token,
                                    TaskCreationOptions.LongRunning,
                                    TaskScheduler.Default).Unwrap();

                        if (options.WebsocketRequiresPong)
                        {
                            _heartbeatWatcher = new HeartbeatWatcher(options.WebsocketTimeout, async () =>
                            {
                                IsReady = false;

                                if (LoggingEnabled)
                                    logger?.LogInformation("Socket timed out.");
                            });
                        }

                        foreach (var kvp in _streams.Values)
                        {
                            if (kvp.Item1.ShouldReconnect)
                            {
                                var request = new StreamInitMessage(kvp!.Item1.RequestData!.Id, connectionId, kvp!.Item1.RequestData.Request);
                                await SendMessageAsync(request!);
                            }
                            else
                            {
                                kvp.Item1.OnError(new HubconRemoteException(
                                    "Websocket connection lost. The subscription was not configured for reconnection."));
                            }
                        }

                        await context.InterceptorManager.CallInterceptor(InterceptorType.OnConnected, _cts.Token);
                        return;
                    }
                    catch (Exception ex)
                    {
                        await context.InterceptorManager.CallInterceptor(InterceptorType.OnError, _cts.Token);

                        _errorStream.OnNext(ex);

                        if (LoggingEnabled)
                            logger?.LogError(ex.Message);

                        foreach (var item in _ingests)
                        {
                            _ingests.TryRemove(item.Key, out _);
                            item.Value.Item1.TrySetCanceled();
                            item.Value.Item2.Cancel();
                        }

                        //foreach (var item in _ingestAck)
                        //{
                        //    _ingestAck.TryRemove(item.Key, out _);
                        //    item.Value.TrySetCanceled();
                        //}

                        //foreach (var item in _ingestDataAck)
                        //{
                        //    _ingestDataAck.TryRemove(item.Key, out _);
                        //    item.Value.TrySetCanceled();
                        //}

                        foreach (var item in _requestsCts.Values)
                        {
                            item.TrySetCanceled();
                        }

                        _requestsCts.Clear();

                        int delay = Math.Min(1 * ++attempt, 30);

                        if (LoggingEnabled)
                            logger?.LogInformation($"Reconectando en {delay} segundos...");

                        await Task.Delay(delay * 1000, _cts.Token);

                        if (_webSocket?.State != WebSocketState.None)
                            _webSocket?.Dispose();
                    }
                }
            }
            finally
            {
                IsReady = true;
                _reconnectLock.Release();
            }
        }

        private void CancelAll()
        {
            _websocketCts?.Cancel();
            _websocketCts?.Dispose();
            _websocketCts = null;

            _receiveLoopCts?.Cancel();
            _receiveLoopCts?.Dispose();
            _receiveLoopCts = null;

            _sendLoopCts?.Cancel();
            _sendLoopCts?.Dispose();
            _sendLoopCts = null;
        }

        private async ValueTask SendMessageAsync<T>(T message, CancellationToken cancellationToken = default) where T : BaseMessage
        {
            var pipe = new Pipe();
            var writer = new Utf8JsonWriter(pipe.Writer);

            converter.Serialize(writer, message);

            await writer.FlushAsync(cancellationToken);
            await pipe.Writer.CompleteAsync();

            var result = await pipe.Reader.ReadAsync(cancellationToken);
            var buffer = result.Buffer;

            byte[] bytes = buffer.ToArray();
            await pipe.Reader.CompleteAsync();

            await _sendChannel.Writer.WriteAsync(new ByteMessage(bytes, connectionId, cancellationToken), cancellationToken);

            message.Dispose();
        }

        private async void PingMessageLoop(object sender, ElapsedEventArgs e)
        {
            if (!IsReady) return;

            try
            {
                if (LoggingEnabled)
                    logger?.LogInformation($"Ping invocado...");

                if (_webSocket?.State == WebSocketState.Open)
                {
                    await SendMessageAsync(new PingMessage(Guid.NewGuid(), connectionId));

                    if (LoggingEnabled)
                        logger?.LogInformation($"Ping enviado.");
                }

                if (_webSocket != null)
                {
                    await context.InterceptorManager.CallInterceptor(InterceptorType.OnPing, _cts.Token);
                }
            }
            catch (Exception ex)
            {
                await context.InterceptorManager.CallInterceptor(InterceptorType.OnError, _cts.Token);

                if (LoggingEnabled)
                    logger?.LogError($"Error en PingMessageLoop: {ex.Message}");
            }
        }

        int cantidad = 0;

        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            if (LoggingEnabled)
            {
                Interlocked.Increment(ref cantidad);
                logger?.LogInformation($"ReceiveLoop iniciado. Cantidad: {Volatile.Read(ref cantidad)}");
            }

            var socket = _webSocket;

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        if (socket == null) break;

                        var parts = new List<IMemoryOwner<byte>>();
                        int totalBytes = 0;

                        ValueWebSocketReceiveResult result;

                        do
                        {
                            var part = MemoryPool<byte>.Shared.Rent(4096);
                            var segment = part.Memory;

                            result = await socket.ReceiveAsync(segment, cancellationToken);
                            cancellationToken.ThrowIfCancellationRequested();

                            if (result.MessageType != WebSocketMessageType.Binary)
                            {
                                if (result.MessageType == WebSocketMessageType.Close)
                                {
                                    if (!CloseSent)
                                    {
                                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnected", CancellationToken.None);
                                        CancelAll();
                                    }

                                    return;
                                }

                                continue;
                            }

                            if (result.Count < segment.Length)
                                part = new TrimmedMemoryOwner(part, result.Count); // Recorta a lo usado

                            totalBytes += result.Count;
                            parts.Add(part);
                        } while (!result.EndOfMessage);

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

                        await _messageChannel.Writer.WriteAsync(new TrimmedMemoryOwner(finalOwner, totalBytes),
                            cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        if (LoggingEnabled)
                            logger?.LogError("Receive loop: Operation cancelled.");

                        CancelAll();
                    }
                    catch (Exception ex)
                    {
                        if (LoggingEnabled)
                            logger?.LogError(ex.ToString());

                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
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
                            if (_webSocket?.State == WebSocketState.Closed)
                                return;

                            if (buffer.CancellationToken.IsCancellationRequested || buffer.ConnectionId != connectionId)
                                continue;

                            var segment = new ArraySegment<byte>(buffer.Bytes);
                            await _webSocket!.SendAsync(segment, WebSocketMessageType.Binary, true, cancellationToken);
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
            catch
            {
            }
        }

        private async void ReconnectLoop(object sender, ElapsedEventArgs e)
        {
            if (!IsReady) return;

            try
            {
                if (_webSocket?.State != WebSocketState.Open && _webSocket?.State != WebSocketState.Connecting && CloseSent == false)
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

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;

            try
            {
                await Disconnect();

                _disposed = true;
                _cts.Cancel();
            }
            finally
            {
                CancelAll();
            }
        }

        public async Task<HubconResponse<bool>> TryRefreshToken(string token)
        {
            var request = new TokenUpdateMessage(Guid.NewGuid(), connectionId, token);

            if (_webSocket?.State != WebSocketState.Open)
                await EnsureConnectedAsync();

            using var response = await SendAndReceive(request, CancellationToken.None);

            if (response == null)
                throw new HubconGenericException("There was an unknown error or the request timed out.");

            var converted = new TokenUpdateResponseMessage(response);
            var result = HubconResponse.OkT(converted.Result, converted.Message);
            return result;
        }

        public async Task Disconnect()
        {
            var socket = _webSocket;
            if (IsReady == true && CloseSent == false && socket != null && socket.State == WebSocketState.Open)
            {
                IsReady = false;
                CloseSent = true;
                await socket!.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnected", CancellationToken.None);
                CancelAll();
            }
        }
    }
}

#pragma warning restore CS1591