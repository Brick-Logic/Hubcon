using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Client.Abstractions.Models;
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

        private readonly SemaphoreSlim _reconnectLock = new SemaphoreSlim(1, 1);

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private CancellationTokenSource? _websocketCts = new CancellationTokenSource();
        private CancellationTokenSource? _receiveLoopCts;
        private CancellationTokenSource? _sendLoopCts;
        private string connectionId = "";

        private bool _disposed = false;

        private bool IsReady = false;

        public bool IsConnected => IsReady && _webSocket?.State == WebSocketState.Open;

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
        }

        public async Task<IObservable<T>> Stream<T>(IOperationRequest payload, bool remoteCancelEnabled, CancellationToken cancellationToken = default)
        {
            await EnsureConnectedAsync();


            return observable;
        }


        public async Task<T> IngestMultiple<T>(
            IOperationRequest operationRequest,
            bool remoteCancelEnabled,
            IClientOptions? clientOptions = null,
            IOperationOptions? operationOptions = null,
            CancellationToken cancellationToken = default)
        {
            await EnsureConnectedAsync();
            
            
        }

        public async Task SendAsync(IOperationRequest payload, bool remoteCancelEnabled, CancellationToken cancellationToken = default)
        { 
            await EnsureConnectedAsync();

            
        }

        public async Task<T> InvokeAsync<T>(IOperationRequest payload, bool remoteCancelEnabled, bool responseIsWrapped, CancellationToken cancellationToken = default)
        {
            await EnsureConnectedAsync();

            
        }

        public async ValueTask EnsureConnectedAsync(Uri? newUrl = null)
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
                        
                        connectionId = ackMessage.ConnectionId;

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

            await EnsureConnectedAsync();
            
            using var response = await SendAndReceive(request, CancellationToken.None);

            if (response == null)
                return HubconResponse.Fail<bool>("There was an unknown error or the request timed out.");

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