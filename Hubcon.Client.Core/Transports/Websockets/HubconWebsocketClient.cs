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

namespace Hubcon.Client.Core.Transports.Websockets
{
    public sealed class HubconWebSocketClient : IAsyncDisposable
    {
        private readonly TransportContext context;
        private readonly IDynamicConverter converter;
        private readonly IClientOptions options;
        private readonly IServiceProvider serviceProvider;
        private readonly ILogger<HubconWebSocketClient>? logger;
        private IHubconWebSocket? _webSocket;

        public bool CloseSent = false;

        public bool LoggingEnabled { get; set; } = true;
        private Uri _uri;
        public void SetUri(Uri uri) => _uri = uri;

        public Action<ClientWebSocketOptions, IServiceProvider>? WebSocketOptions { get; set; }
        public Func<string?>? AuthorizationTokenProvider { get; set; }

        private readonly SemaphoreSlim _reconnectLock = new SemaphoreSlim(1, 1);

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private readonly AtomicPass _disposedPass = new();

        private bool IsReady = false;

        public bool IsConnected => IsReady && _webSocket?.State == WebSocketState.Open;

        private IPingManager? _pingManager;

        public HubconWebSocketClient(Uri uri, TransportContext context, ILogger<HubconWebSocketClient>? logger = null)
        {
            this.context = context;
            _uri = uri;
            this.logger = logger;
            options = context.ClientOptions;
            converter = context.Converter;
            serviceProvider = context.ProxyServiceProvider;
        }

        public async Task<IObservable<T>> Stream<T>(IOperationRequest request, bool remoteCancelEnabled, CancellationToken cancellationToken = default)
        {
            await EnsureConnectedAsync();

            var streamSession = await _webSocket!.GetStreamSession<T>(request, remoteCancelEnabled, cancellationToken);
            return streamSession.GetObservable();
        }


        public async Task<T?> IngestMultiple<T>(
            IOperationRequest operationRequest,
            bool remoteCancelEnabled,
            IOperationOptions? operationOptions = null,
            CancellationToken cancellationToken = default)
        {
            await EnsureConnectedAsync();
            
            var ingestSession = await _webSocket!.GetIngestSession<HubconResponse<T>>(operationRequest, remoteCancelEnabled, operationOptions, cancellationToken);
            var response = await ingestSession.StartAsync(cancellationToken);
            return response!.Data;
        }

        public async Task SendAsync(IOperationRequest request, bool remoteCancelEnabled, CancellationToken cancellationToken = default)
        { 
            await EnsureConnectedAsync();
            var message = new OperationCallMessage(Guid.NewGuid(), _webSocket!.ConnectionId, converter.SerializeToElement(request));
            await _webSocket!.SendAndReceiveAsync(message, cancellationToken);
        }

        public async Task<T> InvokeAsync<T>(IOperationRequest request, bool remoteCancelEnabled, bool responseIsWrapped, CancellationToken cancellationToken = default)
        {
            await EnsureConnectedAsync();
            var message = new OperationInvokeMessage(Guid.NewGuid(), _webSocket!.ConnectionId, converter.SerializeToElement(request));
            var response = await _webSocket!.SendAndReceiveAsync(message, cancellationToken);

            if(response?.Type != MessageType.operation_response)
                throw new InvalidOperationException("Unexpected response type.");

            var operationResponse = new OperationResponseMessage(response);

            return operationResponse != null 
                ? converter.DeserializeJsonElement<T>(operationResponse.Result)! 
                : throw new InvalidCastException("Failed to deserialize response.");
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

                        if(_webSocket != null)
                        {
                            _pingManager?.Dispose();
                            _pingManager = null;

                            await _webSocket.DisposeAsync();
                            _webSocket = null;
                        }

                        var url = newUrl ?? _uri;
                        _uri = url;

                        _webSocket = new HubconWebSocket(context);

                        context.ClientOptions.WebSocketOptions?.Invoke(_webSocket.WebSocket.Options, serviceProvider);

                        var uriBuilder = new UriBuilder(url);
                        var token = AuthorizationTokenProvider?.Invoke();

                        if (!string.IsNullOrEmpty(token))
                            uriBuilder.AddQueryParameter("access_token", token);

                        await context.InterceptorManager.CallInterceptor(InterceptorType.OnConnecting, _cts.Token);

                        await _webSocket.ConnectAsync(uriBuilder.Uri, _cts.Token);

                        _pingManager = new PingManager(_webSocket, context);
                        _pingManager.Start();

                        return;
                    }
                    catch (Exception ex)
                    {
                        await context.InterceptorManager.CallInterceptor(InterceptorType.OnError, _cts.Token);

                        if (LoggingEnabled)
                            logger?.LogError(ex.Message);

                        if(_webSocket != null)
                        {
                            await _webSocket.DisposeAsync();
                            _webSocket = null;
                        }

                        _pingManager?.Dispose();
                        _pingManager = null;

                        int delay = Math.Min(1 * ++attempt, 30);

                        if (LoggingEnabled)
                            logger?.LogInformation($"Connection failed, retrying in {delay} seconds...");

                        await Task.Delay(delay * 1000, _cts.Token);
                    }
                }
            }
            finally
            {
                IsReady = true;
                _reconnectLock.Release();
            }
        }

        public async ValueTask Disconnect()
        {
            if(_webSocket != null)
            {
                await _webSocket.DisposeAsync();
                _webSocket = null;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (!_disposedPass.TryAcquirePass()) return;

            try
            {
                if(_webSocket != null)
                {
                    await _webSocket.DisposeAsync();
                }
            }
            finally
            {
                _webSocket = null;
                GC.SuppressFinalize(this);
            }
        }

        public async Task<HubconResponse<bool>> TryRefreshToken(string token)
        {
            await EnsureConnectedAsync();

            var request = new TokenUpdateMessage(Guid.NewGuid(), _webSocket!.ConnectionId, token);
            
            using var response = await _webSocket!.SendAndReceiveAsync(request, CancellationToken.None);

            if (response == null)
                return HubconResponse.Fail<bool>("There was an unknown error or the request timed out.");

            var converted = new TokenUpdateResponseMessage(response);
            var result = HubconResponse.OkT(converted.Result, converted.Message);
            return result;
        }
    }
}

#pragma warning restore CS1591