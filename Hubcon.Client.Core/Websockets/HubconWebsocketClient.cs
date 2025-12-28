using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Client.Core.Exceptions;
using Hubcon.Client.Core.Extensions;
using Hubcon.Client.Core.Helpers;
using Hubcon.Shared.Abstractions.Enums;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Core.Serialization;
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
using Hubcon.Shared.Core.Websockets.Messages.Subscriptions;
using Hubcon.Shared.Core.Websockets.Messages.Token;
using Hubcon.Shared.Core.Websockets.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Net.WebSockets;
using System.Reactive.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using System.Threading.RateLimiting;
using System.Timers;

namespace Hubcon.Client.Core.Websockets
{
    public sealed class HubconWebSocketClient : IAsyncDisposable, IUnsubscriber
    {
        private readonly Uri _uri;
        private readonly IDynamicConverter converter;
        private readonly IClientOptions options;
        private readonly IServiceProvider serviceProvider;
        private readonly ILogger<HubconWebSocketClient>? logger;
        private ClientWebSocket? _webSocket;

        public bool LoggingEnabled { get; set; } = true;

        public Action<ClientWebSocketOptions, IServiceProvider>? WebSocketOptions { get; set; }
        public Func<string?>? AuthorizationTokenProvider { get; set; }

        private readonly ConcurrentDictionary<Guid, BaseObservable> _subscriptions = new ConcurrentDictionary<Guid, BaseObservable>();

        private readonly ConcurrentDictionary<Guid, (BaseObservable, CancellationTokenSource, HeartbeatWatcher, CancellationTokenRegistration)> _streams = new ConcurrentDictionary<Guid, (BaseObservable, CancellationTokenSource, HeartbeatWatcher, CancellationTokenRegistration)>();

        private readonly ConcurrentDictionary<Guid, TaskCompletionSource<IngestInitAckMessage>> _ingestAck = new ConcurrentDictionary<Guid, TaskCompletionSource<IngestInitAckMessage>>();

        private readonly ConcurrentDictionary<Guid, (TaskCompletionSource<IngestResultMessage>, CancellationTokenSource, CancellationTokenRegistration)> _ingests = new ConcurrentDictionary<Guid, (TaskCompletionSource<IngestResultMessage>, CancellationTokenSource, CancellationTokenRegistration)>();

        private readonly ConcurrentDictionary<Guid, TaskCompletionSource<IngestDataAckMessage>> _ingestDataAck = new ConcurrentDictionary<Guid, TaskCompletionSource<IngestDataAckMessage>>();

        private readonly ConcurrentDictionary<Guid, TaskCompletionSource<OperationResponseMessage>> _operationTcs = new ConcurrentDictionary<Guid, TaskCompletionSource<OperationResponseMessage>>();

        private readonly ConcurrentDictionary<Guid, TaskCompletionSource<TokenUpdateResponseMessage>> _tokenUpdateTcs = new ConcurrentDictionary<Guid, TaskCompletionSource<TokenUpdateResponseMessage>>();

        private readonly SemaphoreSlim _reconnectLock = new SemaphoreSlim(1, 1);

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private CancellationTokenSource? _websocketCts = new CancellationTokenSource();
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

        public IClientOptions ClientOptions { get; set; }

        private System.Timers.Timer? _pingTimer;
        private System.Timers.Timer? _timeoutTimer;
        private List<Task?> _processingTasks;
        private Task? _receiveTask;
        private Task? _sendTask;

        private Guid _lastPongId = Guid.Empty;
        private DateTime _lastPongTime = DateTime.UtcNow;

        private readonly Channel<TrimmedMemoryOwner> _messageChannel;
        private readonly Channel<byte[]> _sendChannel;

        InvocationContext GeneralContext = new InvocationContext();

        public HubconWebSocketClient(Uri uri, IDynamicConverter converter, IClientOptions options,
            IServiceProvider serviceProvider, ILogger<HubconWebSocketClient>? logger = null)
        {
            GeneralContext.Services = serviceProvider;
            GeneralContext.CancellationToken = _cts.Token;

            _pongStream = new GenericObservable<PongMessage>(converter);
            _errorStream = new GenericObservable<Exception>(converter);
            _uri = uri;
            this.converter = converter;
            this.options = options;
            this.serviceProvider = serviceProvider;
            this.logger = logger;

            _messageChannel = Channel.CreateBounded<TrimmedMemoryOwner>(
                new BoundedChannelOptions(20000 * options.MessageProcessorsCount)
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

        public async Task<IObservable<T>> Subscribe<T>(IOperationRequest payload, bool remoteCancelEnabled, CancellationToken cancellationToken = default)
        {
            var request = new SubscriptionInitMessage(Guid.NewGuid(), converter.SerializeToElement(payload));

            var registration = cancellationToken.Register(async () =>
            {
                if (remoteCancelEnabled)
                    await SendMessageAsync(new CancelMessage(request.Id));
            });

            var observable = new GenericObservable<T>(
                this,
                request.Id,
                converter.SerializeToElement(request),
                RequestType.Subscription,
                converter,
                () => registration.Dispose(),
                options.ReconnectSubscriptions);

            if (!_subscriptions.TryAdd(request.Id, observable))
                throw new InvalidOperationException($"Ya existe una suscripción con Id {request.Id}");

            if (_webSocket?.State != WebSocketState.Open)
                await EnsureConnectedAsync();

            await SendMessageAsync(request, cancellationToken);

            return observable;
        }

        public async Task<IObservable<T>> Stream<T>(IOperationRequest payload, bool remoteCancelEnabled, CancellationToken cancellationToken = default)
        {
            var request = new StreamInitMessage(Guid.NewGuid(), converter.SerializeToElement(payload));


            var tcs = new CancellationTokenSource();


            if (_webSocket?.State != WebSocketState.Open)
                await EnsureConnectedAsync();

            HeartbeatWatcher hw = null!;

            CancellationTokenRegistration registration = cancellationToken.Register(async () =>
            {
                if (remoteCancelEnabled)
                    await SendMessageAsync(new CancelMessage(request.Id));
                _ = hw.DisposeAsync();
                tcs.Cancel();
            });

            hw = new HeartbeatWatcher(TimeSpan.FromSeconds(15000), async () =>
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
                this,
                request.Id,
                converter.SerializeToElement(request),
                RequestType.Subscription,
                converter,
                async () => await hw.DisposeAsync(),
                options.ReconnectStreams);

            if (!_streams.TryAdd(request.Id, (observable, tcs, hw, registration)))
                throw new InvalidOperationException($"Ya existe un stream con Id {request.Id}");

            await SendMessageAsync(request);
            return observable;
        }


        public async Task<IOperationResponse<T>> IngestMultiple<T>(
            IOperationRequest operationRequest,
            bool remoteCancelEnabled,
            IClientOptions? clientOptions = null,
            IOperationOptions? operationOptions = null,
            CancellationToken cancellationToken = default)
        {
            using var cts = new CancellationTokenSource();
            var sourceTasks = new List<Task>();
            var initAckTcs = new TaskCompletionSource<IngestInitAckMessage>();
            var generalTcs = new TaskCompletionSource<IngestResultMessage>();
            var sources = new ConcurrentDictionary<Guid, IAsyncEnumerable<JsonElement>>();
            var initialAckId = Guid.NewGuid();

            using var registration = cancellationToken.Register(async () =>
            {
                if (remoteCancelEnabled)
                    await SendMessageAsync(new CancelMessage(initialAckId));

                cts.Cancel();
                generalTcs.TrySetException(new OperationCanceledException());
            });

            _ingestAck.TryAdd(initialAckId, initAckTcs);
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
                        var stream = EnumerableTools.WrapEnumeratorAsJsonElementEnumerable(obj, cts.Token);
                        sources.TryAdd(id, stream!);
                    }
                }

                PropertyTools.AssignProperty(operationRequest, nameof(operationRequest.Arguments), dict);

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
                    var sourceTask = Task.Run(async () =>
                    {
                        try
                        {
                            var initAckResult = await TimeoutHelper.WaitWithTimeoutAsync(initAckTcs.Task.WaitAsync, options.WebsocketTimeout);

                            if (initAckResult == null || initAckResult.Id != initialAckId)
                                throw new TimeoutException("Timeout o ID incorrecto en IngestInitAck");

                            RateLimiter? limiter = sharedLimiter ?? (useShared == false
                                ? new TokenBucketRateLimiter(operationOptions!.RateBucketOptions!)
                                : null);

                            await foreach (var item in source.Value.WithCancellation(cts.Token))
                            {
                                if (generalTcs.Task.IsCompleted || cts.IsCancellationRequested)
                                    break;


                                var message = new IngestDataMessage(source.Key, converter.SerializeToElement(item));

                                try
                                {
                                    await RateLimiterHelper.AcquireAsync(clientOptions, clientOptions?.RateBucket,
                                        clientOptions?.IngestRateBucket, limiter);

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

                var ingestRequest = new IngestInitMessage(initialAckId, sources.Keys.ToArray(), converter.SerializeToElement(operationRequest));

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
                await allIngests;

                try
                {
                    var whenany = Task.WhenAny(allIngests, generalTcs.Task);
                    await whenany;
                }
                finally
                {
                    registration.Dispose();
                }


                await SendMessageAsync(new IngestCompleteMessage(initialAckId, sources.Keys.ToArray()));

                var result = await TimeoutHelper.WaitWithTimeoutAsync(generalTcs.Task.WaitAsync, options.WebsocketTimeout);

                if (generalTcs.Task.Exception?.InnerException is OperationCanceledException)
                    throw generalTcs.Task.Exception.InnerException;

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
                cts.Cancel();
            }
        }

        public async Task SendAsync(IOperationRequest payload, bool remoteCancelEnabled, CancellationToken cancellationToken = default)
        {
            var request = new OperationCallMessage(Guid.NewGuid(), converter.SerializeToElement(payload));

            if (_webSocket?.State != WebSocketState.Open)
                await EnsureConnectedAsync();

            using var registration = cancellationToken.Register(async () =>
            {
                if (remoteCancelEnabled)
                    await SendMessageAsync(new CancelMessage(request.Id));
            });

            await SendMessageAsync(request, cancellationToken);
        }

        public async Task<IOperationResponse<T>> InvokeAsync<T>(IOperationRequest payload, bool remoteCancelEnabled, CancellationToken cancellationToken = default)
        {
            var request = new OperationInvokeMessage(Guid.NewGuid(), converter.SerializeToElement(payload));
            var tcs = new TaskCompletionSource<OperationResponseMessage>();
            _operationTcs.TryAdd(request.Id, tcs);
            OperationResponseMessage? response = null;

            if (_webSocket?.State != WebSocketState.Open)
                await EnsureConnectedAsync();

            try
            {
                using var registration = cancellationToken.Register(async () =>
                {
                    if (remoteCancelEnabled)
                        await SendMessageAsync(new CancelMessage(request.Id));

                    tcs.TrySetException(new OperationCanceledException());
                });

                await SendMessageAsync(request, CancellationToken.None);

                response = await TimeoutHelper.WaitWithTimeoutAsync(tcs.Task.WaitAsync, options.WebsocketTimeout);
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

            if (tcs.Task.Exception?.InnerException is OperationCanceledException)
                throw tcs.Task.Exception.InnerException;

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

                    switch (message.Type)
                    {
                        case MessageType.pong:
                            if (!options.WebsocketRequiresPong)
                                break;

                            var pongMessage = new PongMessage(tmo.Memory, message.Id, message.Type);

                            if (_lastPongId == pongMessage.Id)
                            {
                                await _webSocket!.CloseAsync(WebSocketCloseStatus.InvalidPayloadData, "Pong error",
                                    default);
                                return;
                            }

                            _lastPongId = pongMessage.Id;
                            _lastPongTime = DateTime.UtcNow;
                            _heartbeatWatcher?.NotifyHeartbeat();
                            _pongStream.OnNext(pongMessage);
                            await ClientOptions.CallInterceptor(InterceptorType.OnPong, GeneralContext);
                            break;

                        case MessageType.subscription_data:
                            var eventData = new SubscriptionDataMessage(tmo.Memory, message.Id, message.Type);
                            if (eventData?.Id != null &&
                                _subscriptions.TryGetValue(eventData.Id, out BaseObservable? sub))
                            {
                                sub.OnNextElement(eventData.Data);
                            }

                            break;

                        case MessageType.stream_data:
                            var streamData = new StreamDataMessage(tmo.Memory, message.Id, message.Type);

                            if (streamData?.Id != null && _streams.TryGetValue(streamData.Id, out var stream))
                            {
                                stream.Item1.OnNextElement(streamData.Data);
                                stream.Item3.NotifyHeartbeat();
                            }

                            break;

                        case MessageType.stream_complete:
                            var streamComplete = new StreamCompleteMessage(tmo.Memory, message.Id, message.Type);

                            if (streamComplete?.Id != null &&
                                _streams.TryGetValue(streamComplete.Id, out var streamCompleteInfo))
                            {
                                streamCompleteInfo.Item1.OnCompleted();
                            }

                            break;

                        case MessageType.error:
                            var errorData = new ErrorMessage(tmo.Memory, message.Id, message.Type);
                            if (errorData?.Id != null && _subscriptions.TryGetValue(errorData.Id, out var subToError))
                            {
                                subToError.OnError(new Exception(errorData.Error));
                            }

                            break;

                        case MessageType.ingest_init_ack:
                            var ingestInitAckMessage = new IngestInitAckMessage(tmo.Memory, message.Id, message.Type);

                            if (ingestInitAckMessage == null) break;

                            if (_ingestAck.TryGetValue(ingestInitAckMessage.Id, out var ingestInitAckTcs))
                            {
                                ingestInitAckTcs.TrySetResult(ingestInitAckMessage);
                            }

                            break;

                        case MessageType.ingest_result:
                            var ingestResultMessage = new IngestResultMessage(tmo.Memory, message.Id, message.Type);

                            if (ingestResultMessage == null) break;

                            if (_ingests.TryGetValue(ingestResultMessage.Id, out var ingestResultMessageTcs))
                            {
                                ingestResultMessageTcs.Item1.TrySetResult(ingestResultMessage);
                            }

                            break;

                        case MessageType.token_update:
                            var tokenUpdateResponseMessage = new TokenUpdateResponseMessage(tmo.Memory, message.Id, message.Type);

                            if (tokenUpdateResponseMessage == null) break;

                            if (_tokenUpdateTcs.TryGetValue(tokenUpdateResponseMessage.Id, out var tokenUpdateResponseTcs))
                            {
                                tokenUpdateResponseTcs.TrySetResult(tokenUpdateResponseMessage);
                            }

                            break;

                        case MessageType.ingest_data_ack:
                            var ingestDataAckMessage = new IngestDataAckMessage(tmo.Memory, message.Id, message.Type);

                            if (ingestDataAckMessage == null) break;

                            if (_ingestDataAck.TryGetValue(ingestDataAckMessage.Id, out var ingestDataAckTcs))
                            {
                                ingestDataAckTcs.TrySetResult(ingestDataAckMessage);
                            }

                            break;

                        case MessageType.operation_response:
                            var operationResponseMessage =
                                new OperationResponseMessage(tmo.Memory, message.Id, message.Type);

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

        public async Task EnsureConnectedAsync()
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
                    await ClientOptions.CallInterceptor(InterceptorType.OnDisconnected, GeneralContext);
                    await ClientOptions.CallInterceptor(InterceptorType.OnReconnect, GeneralContext);
                }

                var context = new InvocationContext();
                context.Services = serviceProvider;
                context.CancellationToken = _cts.Token;

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

                        //_pingLoopCts?.Cancel();
                        //_pingLoopCts?.Dispose();
                        //_pingLoopCts = null;

                        _sendLoopCts?.Cancel();
                        _sendLoopCts?.Dispose();
                        _sendLoopCts = null;
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

                        if (LoggingEnabled)
                            logger?.LogInformation("Intentando conectar...");

                        WebSocketOptions?.Invoke(_webSocket.Options, serviceProvider);

                        var uriBuilder = new UriBuilder(_uri);

                        var token = AuthorizationTokenProvider?.Invoke();

                        if (!string.IsNullOrEmpty(token))
                            uriBuilder.AddQueryParameter("access_token", token);

                        _websocketCts = new CancellationTokenSource();
                        await ClientOptions.CallInterceptor(InterceptorType.OnConnecting, context);
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
                            await _webSocket.CloseAsync(WebSocketCloseStatus.ProtocolError, "", CancellationToken.None);
                            throw new TimeoutException("Connection failed.");
                        }

                        if (connectionResult.MessageType == WebSocketMessageType.Close)
                        {
                            await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server closed", CancellationToken.None);
                            return;
                        }

                        var ack = Encoding.UTF8.GetString(buffer, 0, connectionResult.Count);
                        var ackMessage = converter.DeserializeData<ConnectionAckMessage>(ack);

                        if (ackMessage?.Type != MessageType.connection_ack)
                        {
                            await _webSocket.CloseAsync(WebSocketCloseStatus.ProtocolError, "", CancellationToken.None);
                            throw new Exception("No se recibió el connection_ack del servidor.");
                        }

                        if (ackMessage.Id != msgId)
                        {
                            await _webSocket.CloseAsync(WebSocketCloseStatus.ProtocolError, "", CancellationToken.None);
                            throw new Exception("Se recibió una confirmación, pero el ID no es correcto. Rechazando conexión...");
                        }

                        if (LoggingEnabled)
                            logger?.LogInformation("Confirmación recibida, iniciando loop de respuesta...");

                        _receiveLoopCts = new CancellationTokenSource();

                        if (options.AutoReconnect && _timeoutTimer == null)
                        {
                            _timeoutTimer ??= new System.Timers.Timer(5000);
                            _timeoutTimer.Elapsed += ReconnectLoop;
                            _timeoutTimer.AutoReset = true;
                            _timeoutTimer.Enabled = true;
                            _timeoutTimer.Start();                   
                        }

                        if(_processingTasks == null)
                        {
                            _processingTasks = new List<Task?>();
                            foreach(var number in Enumerable.Range(0, options.MessageProcessorsCount))
                            {
                                _processingTasks.Add(Task.Factory.StartNew(
                                    async () => await HandleIncomingMessage(),
                                    _cts.Token,
                                    TaskCreationOptions.LongRunning,
                                    TaskScheduler.Default).Unwrap());
                            }
                        }

                        if(_pingTimer == null)
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

                        IsReady = true;

                        foreach (var kvp in _subscriptions.Values)
                        {
                            if (kvp.ShouldReconnect)
                            {
                                var request =
                                    converter.DeserializeJsonElement<SubscriptionRequest>(kvp.RequestData!.Request);
                                await SendMessageAsync(request!);
                            }
                            else
                            {
                                kvp.OnError(new HubconRemoteException(
                                    "Websocket connection lost. The subscription was not configured for reconnection."));
                            }
                        }

                        foreach (var kvp in _streams.Values)
                        {
                            if (kvp.Item1.ShouldReconnect)
                            {
                                var request =
                                    converter.DeserializeJsonElement<SubscriptionRequest>(
                                        kvp.Item1.RequestData!.Request);
                                await SendMessageAsync(request!);
                            }
                            else
                            {
                                kvp.Item1.OnError(new HubconRemoteException(
                                    "Websocket connection lost. The subscription was not configured for reconnection."));
                            }
                        }
                        await ClientOptions.CallInterceptor(InterceptorType.OnConnected, context);
                        return;
                    }
                    catch (Exception ex)
                    {
                        context.Exception = ex;
                        await ClientOptions.CallInterceptor(InterceptorType.OnError, context);

                        _errorStream.OnNext(ex);

                        if (LoggingEnabled)
                            logger?.LogError(ex.Message);

                        foreach (var item in _ingests)
                        {
                            _ingests.TryRemove(item.Key, out _);
                            item.Value.Item1.TrySetCanceled();
                            item.Value.Item2.Cancel();
                        }

                        foreach (var item in _ingestAck)
                        {
                            _ingestAck.TryRemove(item.Key, out _);
                            item.Value.TrySetCanceled();
                        }

                        foreach (var item in _ingestDataAck)
                        {
                            _ingestDataAck.TryRemove(item.Key, out _);
                            item.Value.TrySetCanceled();
                        }

                        int delay = Math.Min(1 * ++attempt, 30);

                        if (LoggingEnabled)
                            logger?.LogInformation($"Reconectando en {delay} segundos...");

                        await Task.Delay(delay * 1000, _cts.Token);
                    }
                }
            }
            finally
            {
                _reconnectLock.Release();
            }
        }

        private async ValueTask SendMessageAsync<T>(T message, CancellationToken cancellationToken = default)
        {
            var pipe = new Pipe();
            var writer = new Utf8JsonWriter(pipe.Writer);

            JsonSerializer.Serialize(writer, message, DynamicConverter.JsonSerializerOptions);
            await writer.FlushAsync(cancellationToken);
            await pipe.Writer.CompleteAsync();

            var result = await pipe.Reader.ReadAsync(cancellationToken);
            var buffer = result.Buffer;

            byte[] bytes = buffer.ToArray();
            await pipe.Reader.CompleteAsync();

            await _sendChannel.Writer.WriteAsync(bytes, cancellationToken);
        }

        private async void PingMessageLoop(object sender, ElapsedEventArgs e)
        {
            if(!IsReady) return;

            var context = new InvocationContext();
            context.Services = serviceProvider;
            context.CancellationToken = _cts.Token;
            context.TryRefreshToken = TryRefreshToken;

            try
            {
                if (LoggingEnabled)
                    logger?.LogError($"Ping invocado...");

                if (_webSocket?.State == WebSocketState.Open)
                {
                    await SendMessageAsync(new PingMessage(Guid.NewGuid()));

                    if (LoggingEnabled)
                        logger?.LogError($"Ping enviado.");
                }

                if(_webSocket != null)
                {
                    await ClientOptions.CallInterceptor(InterceptorType.OnPing, context);
                }
            }
            catch (Exception ex)
            {
                await ClientOptions.CallInterceptor(InterceptorType.OnError, context);

                if (LoggingEnabled)
                    logger?.LogError($"Error en PingMessageLoop: {ex.Message}");
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
                                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server closed",
                                    CancellationToken.None);
                                throw new OperationCanceledException();
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
                            if (_webSocket?.State != WebSocketState.Open)
                                await EnsureConnectedAsync();

                            var segment = new ArraySegment<byte>(buffer);
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
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
            }
        }

        private async void ReconnectLoop(object sender, ElapsedEventArgs e)
        {
            if (!IsReady) return;

            try
            {
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

                _pingTimer?.Stop();
                _pingTimer?.Dispose();

                _timeoutTimer?.Stop();
                _timeoutTimer?.Dispose();
            }
            finally
            {
                /*Ignore*/
            }
        }

        public async Task<IOperationResponse<bool>> TryRefreshToken(string token)
        {
            var request = new TokenUpdateMessage(Guid.NewGuid(), token);
            var tcs = new TaskCompletionSource<TokenUpdateResponseMessage>();
            _tokenUpdateTcs.TryAdd(request.Id, tcs);
            TokenUpdateResponseMessage? response = null;

            if (_webSocket?.State != WebSocketState.Open)
                await EnsureConnectedAsync();

            try
            {
                await SendMessageAsync(request, CancellationToken.None);
                response = await TimeoutHelper.WaitWithTimeoutAsync(tcs.Task.WaitAsync, options.WebsocketTimeout);
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

            if (tcs.Task.Exception?.InnerException is OperationCanceledException)
                throw tcs.Task.Exception.InnerException;

            if (response == null)
                throw new HubconGenericException("There was an unknown error or the request timed out.");

            var converted = new BaseOperationResponse<bool>(response.Result, default, response.Message);

            return converted;
        }

        public async Task Disconnect()
        {
            IsReady = false;
            if(_webSocket != null)
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "User disconnected.", CancellationToken.None);
                _webSocket?.Dispose();
                _websocketCts?.Cancel();            
            }
        }
    }
}