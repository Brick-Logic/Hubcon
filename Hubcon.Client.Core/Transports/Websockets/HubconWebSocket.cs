using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Client.Abstractions.Models;
using Hubcon.Client.Core.Extensions;
using Hubcon.Client.Core.Helpers;
using Hubcon.Client.Core.Transports.Websockets.Managers;
using Hubcon.Client.Core.Transports.Websockets.MessageHandlers;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Core.Extensions;
using Hubcon.Shared.Core.Tools;
using Hubcon.Shared.Core.Websockets;
using Hubcon.Shared.Core.Websockets.Events;
using Hubcon.Shared.Core.Websockets.Heartbeat;
using Hubcon.Shared.Core.Websockets.Interfaces;
using Hubcon.Shared.Core.Websockets.Messages.Cancellation;
using Hubcon.Shared.Core.Websockets.Messages.Connection;
using Hubcon.Shared.Core.Websockets.Messages.Generic;
using Hubcon.Shared.Core.Websockets.Messages.Ingest;
using Hubcon.Shared.Core.Websockets.Messages.Ping;
using Hubcon.Shared.Core.Websockets.Messages.Streams;
using Hubcon.Shared.Core.Websockets.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hubcon.Client.Core.Transports.Websockets
{
    /// <summary>
    /// Manages 
    /// </summary>
    internal sealed class HubconWebSocket : IHubconWebSocket
    {
        private readonly AtomicPass _disposedPass = new();
        private readonly AtomicPass _connectionPass = new();

        private readonly CancellationTokenSource _cts;

        private readonly TransportContext _context;
        private readonly ILogger<HubconWebSocket>? _logger;
        private readonly IClientOptions _options;
        private readonly IDynamicConverter _converter;
        private readonly IClientOptions _clientOptions;
        private readonly IServiceProvider _serviceProvider;

        private readonly ClientWebSocket _webSocket;
        private readonly MessageSender _sender;
        private readonly MessageReceiver _receiver;
        private string connectionId = string.Empty;

        private readonly bool _loggingEnabled;

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="context"></param>
        public HubconWebSocket(TransportContext context)
        {
            _cts = new CancellationTokenSource();
            _context = context;

            _webSocket = new ClientWebSocket();

            _options = context.ClientOptions;
            _converter = context.Converter;
            _clientOptions = context.ClientOptions;
            _serviceProvider = context.ProxyServiceProvider;
            _logger = context.ProxyServiceProvider.GetService<ILogger<HubconWebSocket>>();

            _loggingEnabled = _options.LoggingEnabled;

            _receiver = new MessageReceiver(this, context);
            _sender = new MessageSender(this, context);
        }

        /// <summary>
        /// The connection's current ID.
        /// </summary>
        public string ConnectionId => connectionId;

        public IMessageSender Sender => _sender;
        public IMessageReceiver Receiver => _receiver;

        public WebSocketState State => _webSocket.State;

        public ClientWebSocket WebSocket => _webSocket;

        /// <summary>
        /// Sends a message excepting a response. 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="useRemoteCancel"></param>
        /// <param name="cancellationToken"></param>
        /// <typeparam name="TRequest"></typeparam>
        /// <returns></returns>
        public async Task<BaseMessage?> SendAndReceiveAsync<TRequest>(TRequest message, bool useRemoteCancel, CancellationToken cancellationToken = default) where TRequest : BaseMessage
        {
            cancellationToken.ThrowIfCancellationRequested();
            Throw.If(_disposedPass.WasAcquired, "This object has been disposed.");
            Throw.IfNot(_connectionPass.WasAcquired, "The connection has not been established.");
            Throw.IfNotEqual(_webSocket?.State, WebSocketState.Open, $"WebSocket is not open. The WebSocket might be in a closed or faulted state and must be disposed. Current state: {_webSocket?.State}");
            using var localCts = new CancellationTokenSource();
            using var registration1 = cancellationToken.Register(() => localCts.Cancel());
            using var registration2 = _cts.Token.Register(() => localCts.Cancel());
            CancellationTokenRegistration? registration = null; 

            try
            {
                if (useRemoteCancel)
                {
                    registration = cancellationToken.Register(async () => await _sender.SendMessageAsync(new CancelMessage(message.Id, connectionId)));
                }

                _receiver.Router.BeginRequest(message.Id);

                await _sender.SendMessageAsync(message, localCts.Token);
                var response = await _receiver.Router.GetResponseAsync(message.Id, _clientOptions.WebsocketTimeout, localCts.Token);

                if(useRemoteCancel || response == null)
                {
                    response = await _receiver.Router.GetResponseAsync(message.Id, _clientOptions.WebsocketTimeout, CancellationToken.None);
                }

                return response;
            }
            finally
            {
                registration?.Dispose();
                _receiver.Router.EndRequest(message.Id);
            }
        }

        /// <summary>
        /// Sends a message excepting a response. 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="useRemoteCancel"></param>
        /// <param name="cancellationToken"></param>
        /// <typeparam name="TRequest"></typeparam>
        /// <returns></returns>
        public async Task SendAsync<TRequest>(TRequest message, bool useRemoteCancel, CancellationToken cancellationToken = default) where TRequest : BaseMessage
        {
            cancellationToken.ThrowIfCancellationRequested();
            Throw.If(_disposedPass.WasAcquired, "This object has been disposed.");
            Throw.IfNot(_connectionPass.WasAcquired, "The connection has not been established.");
            Throw.IfNotEqual(_webSocket?.State, WebSocketState.Open, $"WebSocket is not open. The WebSocket might be in a closed or faulted state and must be disposed. Current state: {_webSocket?.State}");

            using var localCts = new CancellationTokenSource();
            using var registration1 = cancellationToken.Register(() => localCts.Cancel());
            using var registration2 = _cts.Token.Register(() => localCts.Cancel());

            await _sender.SendMessageAsync(message, localCts.Token);
        }

        /// <summary>
        /// Creates and returns an object that handles a streaming session.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="payload"></param>
        /// <param name="remoteCancelEnabled"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<IStreamSession<T>> GetStreamSession<T>(IOperationRequest payload, bool remoteCancelEnabled, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Throw.If(_disposedPass.WasAcquired, "This object has been disposed.");
            Throw.IfNot(_connectionPass.WasAcquired, "The connection has not been established.");
            Throw.IfNotEqual(_webSocket?.State, WebSocketState.Open, $"WebSocket is not open. The WebSocket might be in a closed or faulted state and must be disposed. Current state: {_webSocket?.State}");

            var localCts = new CancellationTokenSource();
            var registration1 = cancellationToken.Register(() => localCts.Cancel());
            var registration2 = _cts.Token.Register(() => localCts.Cancel());

            var streamSession = _receiver.Router.CreateStream<T>(Guid.NewGuid(), connectionId, payload, () =>
            {
                registration1.Dispose();
                registration2.Dispose();
                localCts.Dispose();
            });

            var request = streamSession.Payload;

            if (remoteCancelEnabled)
            {
                streamSession.AddCancellation(async () =>
                {
                    if (remoteCancelEnabled && _webSocket.State == WebSocketState.Open)
                        await _sender.SendMessageAsync(new CancelMessage(request.Id, connectionId), cancellationToken);

                    streamSession.TryComplete();
                    streamSession.Dispose();
                }, cancellationToken);
            }

            await _sender.SendMessageAsync(request, cancellationToken);

            return streamSession;
        }

        /// <summary>
        /// Creates and returns an object that handles an ingest session.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="operationRequest"></param>
        /// <param name="remoteCancelEnabled"></param>
        /// <param name="operationOptions"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<IIngestSession<T>> GetIngestSession<T>(
            IOperationRequest operationRequest,
            bool remoteCancelEnabled,
            IOperationOptions? operationOptions = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Throw.If(_disposedPass.WasAcquired, "This object has been disposed.");
            Throw.IfNot(_connectionPass.WasAcquired, "The connection has not been established.");
            Throw.IfNotEqual(_webSocket?.State, WebSocketState.Open, $"WebSocket is not open. The WebSocket might be in a closed or faulted state and must be disposed. Current state: {_webSocket?.State}");

            var localCts = new CancellationTokenSource();
            var registration1 = cancellationToken.Register(() => localCts.Cancel());
            var registration2 = _cts.Token.Register(() => localCts.Cancel());

            var ingestSession = _receiver.Router.CreateIngest<T>(Guid.NewGuid(), connectionId, operationRequest, operationOptions!, () =>
            {
                registration1.Dispose();
                registration2.Dispose();
                localCts.Dispose();
            });

            if (remoteCancelEnabled)
            {
                ingestSession.AddCancellation(async () =>
                {
                    if (remoteCancelEnabled && _webSocket.State == WebSocketState.Open)
                        await _sender.SendMessageAsync(new CancelMessage(ingestSession.Id, connectionId), localCts.Token);

                    ingestSession.Dispose();
                }, cancellationToken);
            }

            return ingestSession;
        }

        public async Task ConnectAsync(Uri uri, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Throw.If(_disposedPass.WasAcquired, "This object has been disposed.");
            Throw.IfNot(_connectionPass.TryAcquirePass(), "The hubcon websocket can only be connected once.");

            if (_loggingEnabled)
                _logger?.LogInformation("Trying to connect to the server...");

            await _webSocket.ConnectAsync(uri, cancellationToken);

            _receiver.Start();

            if (_loggingEnabled)
                _logger?.LogInformation("Connected, attempting handshake...");

            var msgId = Guid.NewGuid();
            using var connectionResponse = await SendAndReceiveAsync(new ConnectionInitMessage(msgId), false);

            Throw.If(connectionResponse == null, "Handshake failed: No response received or the request timed out.");
            Throw.If(connectionResponse?.Type != MessageType.connection_ack, $"Handshake failed: Expected '{nameof(MessageType.connection_ack)}' but received '{connectionResponse?.Type}'.");
            Throw.If(connectionResponse?.Id != msgId, $"Handshake failed: Message ID mismatch. Expected '{msgId}' but received '{connectionResponse?.Id}'.");

            using var ackMessage = new ConnectionAckMessage(connectionResponse!);

            Throw.If(string.IsNullOrWhiteSpace(ackMessage.ConnectionId), "Handshake failed: Invalid connection ID received.");
            Throw.If(!string.IsNullOrWhiteSpace(ackMessage.Error), $"Handshake returned an error: {ackMessage.Error}");

            connectionId = ackMessage.ConnectionId;

            if (_loggingEnabled)
                _logger?.LogInformation("Connection established.");

            await _context.InterceptorManager.CallInterceptor(InterceptorType.OnConnected, _cts.Token);
        }

        public async ValueTask DisposeAsync()
        {
            if (!_disposedPass.TryAcquirePass()) return;

            await _sender.DisposeAsync();
            await _receiver.DisposeAsync();

            _cts.Dispose();
            _webSocket.Dispose();

            GC.SuppressFinalize(this);
        }
    }
}