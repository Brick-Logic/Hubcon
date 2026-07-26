using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Client.Abstractions.Models;
using Hubcon.Client.Core.Extensions;
using Hubcon.Client.Core.Helpers;
using Hubcon.Client.Core.Transports.Websockets.Managers;
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
using Hubcon.Shared.Core.Extensions;
using Timer = System.Timers.Timer;

#pragma warning disable CS1591

namespace Hubcon.Client.Core.Transports.Websockets
{
    public sealed class HubconWebSocketClient : IAsyncDisposable
    {
        private readonly TransportContext context;
        private readonly IDynamicConverter converter;
        private readonly IServiceProvider serviceProvider;
        private readonly ILogger<HubconWebSocketClient>? logger;
        private IHubconWebSocket? _webSocket;

        private bool LoggingEnabled { get; } = true;
        private Uri _uri;

        public Action<ClientWebSocketOptions, IServiceProvider>? WebSocketOptions { get; set; }
        public Func<IAuthenticationManager>? AuthenticationManagerProvider { get; set; }

        private readonly SemaphoreSlim _reconnectLock = new SemaphoreSlim(1, 1);

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private readonly AtomicPass _disposedPass = new();

        private bool isReady;

        public bool IsConnected => isReady && _webSocket?.State == WebSocketState.Open;

        private IPingManager? _pingManager;

        private readonly Timer? _reconnectTimer;
        private readonly SemaphoreSlim _reconnectSemaphore = new SemaphoreSlim(1, 1);

        public HubconWebSocketClient(Uri uri, TransportContext context, ILogger<HubconWebSocketClient>? logger = null)
        {
            this.context = context;
            _uri = uri;
            this.logger = logger;
            converter = context.Converter;
            serviceProvider = context.ProxyServiceProvider;

            if (context.ClientOptions.AutoReconnect)
            {
                _reconnectTimer = new Timer();
                _reconnectTimer.AutoReset = true;
                _reconnectTimer.Enabled = true;
                _reconnectTimer.Interval = 3000;
                _reconnectTimer.Elapsed += async (_, _) => await ReconnectTimerOnElapsed();
            }
        }

        private async Task ReconnectTimerOnElapsed()
        {
            if (!isReady) return;

            if (_disposedPass.WasAcquired) return;

            if (!await _reconnectSemaphore.WaitAsync(0))
                return;

            if (_webSocket?.State is WebSocketState.Open)
                return;

            if (LoggingEnabled) logger?.LogInformation("Hubcon WebSocket is disconnected, trying to reconnect...");

            await EnsureConnectedAsync();

            _reconnectSemaphore.Release();
        }

        public async Task<IObservable<T>> Stream<T>(IOperationRequest request, bool remoteCancelEnabled,
            CancellationToken cancellationToken = default)
        {
            await EnsureConnectedAsync();

            var streamSession = await _webSocket!.GetStreamSession<T>(request, remoteCancelEnabled, cancellationToken);

            return streamSession.GetObservable();
        }


        public async Task<IHubconResponse<T>?> IngestMultiple<T>(
            IOperationRequest operationRequest,
            bool remoteCancelEnabled,
            IOperationOptions? operationOptions = null,
            CancellationToken cancellationToken = default)
        {
            await EnsureConnectedAsync();

            using var ingestSession = _webSocket!.GetIngestSession<HubconResponse<T>>(operationRequest,
                remoteCancelEnabled, operationOptions, cancellationToken);
            var response = await ingestSession.StartAsync(cancellationToken);
            return response;
        }

        public async Task SendAsync(IOperationRequest request, bool remoteCancelEnabled,
            CancellationToken cancellationToken = default)
        {
            await EnsureConnectedAsync();

            using var message = new OperationCallMessage(Guid.NewGuid(), _webSocket!.ConnectionId,
                converter.SerializeToElement(request));
            await _webSocket!.SendAndReceiveAsync(message, remoteCancelEnabled, cancellationToken);
        }

        public async Task<T> InvokeAsync<T>(IOperationRequest request, bool remoteCancelEnabled, bool responseIsWrapped,
            CancellationToken cancellationToken = default)
        {
            await EnsureConnectedAsync();

            using var message = new OperationInvokeMessage(Guid.NewGuid(), _webSocket!.ConnectionId,
                converter.SerializeToElement(request));
            using var response = await _webSocket!.SendAndReceiveAsync(message, remoteCancelEnabled, cancellationToken);

            if (response?.Type != MessageType.operation_response)
                throw new InvalidOperationException("Unexpected response type.");

            using var operationResponse = new OperationResponseMessage(response);

            return operationResponse != null
                ? converter.DeserializeJsonElement<T>(operationResponse.Result)!
                : throw new InvalidCastException("Failed to deserialize response.");
        }

        public async ValueTask EnsureConnectedAsync(Uri? newUrl = null)
        {
            if (_webSocket?.State is WebSocketState.Open) return;

            Throw.If(_disposedPass.WasAcquired,
                static () => new ObjectDisposedException("This object has already been disposed."));

            await _reconnectLock.WaitAsync();

            try
            {
                switch (_webSocket?.State)
                {
                    case WebSocketState.Open:
                        return;
                    case WebSocketState.Closed or WebSocketState.CloseReceived or WebSocketState.CloseSent:
                        await context.InterceptorManager.CallInterceptor(InterceptorType.OnReconnect, _cts.Token);
                        break;
                }

                var attempt = 0;
                while (!_cts.IsCancellationRequested)
                {
                    Throw.If(_disposedPass.WasAcquired,
                        static () => new ObjectDisposedException("This object has already been disposed."));

                    try
                    {
                        isReady = false;

                        _pingManager?.Dispose();
                        _pingManager = null;

                        if (_webSocket != null)
                        {
                            await _webSocket.DisposeAsync();
                            _webSocket = null;
                        }

                        var url = newUrl ?? _uri;
                        _uri = url;

                        _webSocket = new HubconWebSocket(context);

                        _webSocket.Receiver.OnDisconnected += async () =>
                        {
                            isReady = false;
                            await _webSocket.DisposeAsync();
                        };

                        context.ClientOptions.WebSocketOptions?.Invoke(_webSocket.WebSocket.Options, serviceProvider);

                        var uriBuilder = new UriBuilder(url);
                        var authManager = AuthenticationManagerProvider?.Invoke();

                        if (authManager is { IsSessionActive: true })
                        {
                            var authToken = "";
                            if (!string.IsNullOrEmpty(authManager.TokenType))
                                authToken += authManager.TokenType + " ";
                            if (!string.IsNullOrEmpty(authManager.AccessToken))
                                authToken += authManager.AccessToken;

                            uriBuilder.AddQueryParameter("access_token", authToken);
                        }

                        await context.InterceptorManager.CallInterceptor(InterceptorType.OnConnecting, _cts.Token);

                        await _webSocket.ConnectAsync(uriBuilder.Uri, _cts.Token);

                        _pingManager = new PingManager(_webSocket, context);
                        _pingManager.Start();

                        _reconnectTimer?.Start();

                        return;
                    }
                    catch (Exception ex)
                    {
                        await context.InterceptorManager.CallInterceptor(InterceptorType.OnError, _cts.Token);

                        if (LoggingEnabled)
                        {
                            ex.TryExtractStatusCode(out var value);

                            switch (value)
                            {
                                case 401:
                                    logger?.LogError("WebSocket connection could not be authenticated.");
                                    break;
                                case 403:
                                    logger?.LogError("WebSocket connection is missing the Origin header or has an incorrect value.");
                                    break;
                                default:
                                    logger?.LogError(ex.Message);
                                    break;
                            }
                        }

                        if (_webSocket != null)
                        {
                            await _webSocket.DisposeAsync();
                            _webSocket = null;
                        }

                        _pingManager?.Dispose();
                        _pingManager = null;

                        var delay = Math.Min(1 * ++attempt, 30);

                        if (LoggingEnabled)
                            logger?.LogInformation($"Reconnecting in {delay} seconds...");

                        await Task.Delay(delay * 1000, _cts.Token);
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                throw;
            }
            finally
            {
                isReady = true;
                _reconnectLock.Release();
            }
        }

        public async ValueTask Disconnect()
        {
            isReady = false;

            if (_webSocket != null)
            {
                await _webSocket.DisconnectAsync();
                await _webSocket.DisposeAsync();
                _webSocket = null;
            }
        }

        public async Task<HubconResponse<bool>> TryRefreshToken(string token)
        {
            await EnsureConnectedAsync();

            var request = new TokenUpdateMessage(Guid.NewGuid(), _webSocket!.ConnectionId, token);

            using var response = await _webSocket!.SendAndReceiveAsync(request, false, CancellationToken.None);

            if (response == null)
                return HubconResponse.Fail<bool>("There was an unknown error or the request timed out.");

            var converted = new TokenUpdateResponseMessage(response);
            var result = HubconResponse.OkT(converted.Result, converted.Message);
            return result;
        }

        public async ValueTask DisposeAsync()
        {
            if (!_disposedPass.TryAcquirePass()) return;

            try
            {
                isReady = false;

                if (_webSocket != null)
                {
                    _reconnectTimer?.Dispose();
                    _reconnectSemaphore.Dispose();
                    _reconnectLock.Dispose();
                    _pingManager?.Dispose();
                    await _webSocket.DisposeAsync();
                }
            }
            finally
            {
                _webSocket = null;
                GC.SuppressFinalize(this);
            }
        }
    }
}

#pragma warning restore CS1591