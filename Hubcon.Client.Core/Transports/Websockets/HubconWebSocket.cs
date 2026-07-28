using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Client.Core.Transports.Websockets.MessageHandlers;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Core.Extensions;
using Hubcon.Shared.Core.Tools;
using Hubcon.Shared.Core.Websockets;
using Hubcon.Shared.Core.Websockets.Messages.Cancellation;
using Hubcon.Shared.Core.Websockets.Messages.Connection;
using Hubcon.Shared.Core.Websockets.Messages.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hubcon.Client.Core.Transports.Websockets
{
    /// <summary>
    /// Implements the WebSocket client communication logic for Hubcon.
    /// This class handles connection management, message sending, receiving, routing, streaming and ingesting
    /// over a WebSocket transport.
    /// </summary>
    internal sealed class HubconWebSocket : IHubconWebSocket
    {
        private readonly AtomicPass _disposedPass = new();
        private readonly AtomicPass _connectionPass = new();
        private readonly AtomicPass _disconnectionPass = new();

        private readonly CancellationTokenSource _cts;

        private readonly TransportContext _context;
        private readonly ILogger<HubconWebSocket>? _logger;
        private readonly IDynamicConverter _converter;
        private readonly IClientOptions _clientOptions;
        private readonly IServiceProvider _serviceProvider;

        private readonly MessageSender _sender;
        private readonly MessageReceiver _receiver;
        private string connectionId = string.Empty;

        private readonly bool _loggingEnabled;


        public HubconWebSocket(TransportContext context)
        {
            _cts = new CancellationTokenSource();
            _context = context;

            WebSocket = new ClientWebSocket();

            _converter = context.Converter;
            _clientOptions = context.ClientOptions;
            _serviceProvider = context.ProxyServiceProvider;
            _logger = context.ProxyServiceProvider.GetService<ILogger<HubconWebSocket>>();

            _loggingEnabled = context.ClientOptions.LoggingEnabled;

            _receiver = new MessageReceiver(this, context);
            _receiver.OnCloseReceived += async () => await DisconnectAsync();
            _sender = new MessageSender(this, context);
        }

        /// <summary>
        /// The connection's current ID.
        /// </summary>
        public string ConnectionId => connectionId;

        /// <inheritdoc/>
        public IMessageSender Sender => _sender;

        /// <inheritdoc/>
        public IMessageReceiver Receiver => _receiver;

        /// <inheritdoc/>
        public WebSocketState State => WebSocket.State;

        /// <inheritdoc/>
        public ClientWebSocket WebSocket { get; }

        /// <inheritdoc/>
        public ValueTask<BaseMessage?> SendAndReceiveAsync<TRequest>(TRequest message, bool useRemoteCancel,
            CancellationToken cancellationToken = default) where TRequest : BaseMessage
        {
            return SendAndReceiveAsync(message, useRemoteCancel, _clientOptions.WebsocketTimeout, cancellationToken);
        }

        /// <inheritdoc/>
        public async ValueTask<BaseMessage?> SendAndReceiveAsync<TRequest>(TRequest message, bool useRemoteCancel,
            TimeSpan timeout, CancellationToken cancellationToken = default) where TRequest : BaseMessage
        {
            cancellationToken.ThrowIfCancellationRequested();

            Throw.If(_disposedPass.WasAcquired, static ()
                => new HubconGenericException("The HubconWebSocket has been disposed."));

            Throw.If(_disconnectionPass.WasAcquired, static ()
                => new HubconGenericException(
                    "The HubconWebSocket has already been disconnected and must be disposed."));

            Throw.IfNot(_connectionPass.WasAcquired, "The connection has not been established.");

            Throw.IfNotEqual(WebSocket?.State, WebSocketState.Open,
                "The HubconWebSocket is not open, it is in a closed or faulted state and must be disposed.");

            using var localCts = new CancellationTokenSource();
            await using var reg1 = cancellationToken.Register(CancelCtsDelegate, localCts);
            await using var reg2 = _cts.Token.Register(CancelCtsDelegate, localCts);

            try
            {
                _receiver.Router.BeginRequest(message.Id);

                await _sender.SendMessageAsync(message, localCts.Token);
                var response = await _receiver.Router.GetResponseAsync(message.Id, timeout, localCts.Token);

                if (!useRemoteCancel || !cancellationToken.IsCancellationRequested || response != null) return response;

                await _sender.SendMessageAsync(new CancelMessage(message.Id, connectionId), _cts.Token);
                response = await _receiver.Router.GetResponseAsync(message.Id, timeout, CancellationToken.None);

                return response;
            }
            finally
            {
                _receiver.Router.EndRequest(message.Id);
            }
        }

        /// <inheritdoc/>
        public async ValueTask SendAsync<TRequest>(TRequest message, bool useRemoteCancel,
            CancellationToken cancellationToken = default) where TRequest : BaseMessage
        {
            cancellationToken.ThrowIfCancellationRequested();

            Throw.If(_disposedPass.WasAcquired, static ()
                => new HubconGenericException("The HubconWebSocket has been disposed."));

            Throw.If(_disconnectionPass.WasAcquired, static ()
                => new HubconGenericException(
                    "The HubconWebSocket has already been disconnected and must be disposed."));

            Throw.IfNot(_connectionPass.WasAcquired, "The connection has not been established.");

            Throw.IfNotEqual(WebSocket?.State, WebSocketState.Open,
                "The HubconWebSocket is not open, it is in a closed or faulted state and must be disposed.");

            using var localCts = new CancellationTokenSource();
            await using var reg1 = cancellationToken.Register(CancelCtsDelegate, localCts);
            await using var reg2 = _cts.Token.Register(CancelCtsDelegate, localCts);

            await _sender.SendMessageAsync(message, localCts.Token);
        }

        /// <inheritdoc/>
        public async ValueTask<IStreamSession<T>> GetStreamSession<T>(IOperationRequest payload,
            bool remoteCancelEnabled, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Throw.If(_disposedPass.WasAcquired, static ()
                => new HubconGenericException("The HubconWebSocket has been disposed."));

            Throw.If(_disconnectionPass.WasAcquired, static ()
                => new HubconGenericException(
                    "The HubconWebSocket has already been disconnected and must be disposed."));

            Throw.IfNot(_connectionPass.WasAcquired, "The connection has not been established.");

            Throw.IfNotEqual(WebSocket?.State, WebSocketState.Open,
                "The HubconWebSocket is not open, it is in a closed or faulted state and must be disposed.");

            var localCts = new CancellationTokenSource();
            var reg1 = cancellationToken.Register(CancelCtsDelegate, localCts);
            var reg2 = _cts.Token.Register(CancelCtsDelegate, localCts);

            var streamSession = _receiver.Router.CreateStream<T>(Guid.NewGuid(), connectionId, payload, async () =>
            {
                localCts.Dispose();
                await reg1.DisposeAsync();
                await reg2.DisposeAsync();
            });

            var request = streamSession.Payload;

            if (remoteCancelEnabled)
            {
                streamSession.AddCancellation(async state =>
                    {
                        var (streamSessionState, remoteCancelEnabledState, webSocketState, senderState, requestId,
                                connectionIdState, ct)
                            = (((IStreamSession, bool, WebSocketState, IMessageSender, Guid, string, CancellationToken))
                                state!);

                        if (remoteCancelEnabledState && webSocketState == WebSocketState.Open)
                            await senderState.SendMessageAsync(new CancelMessage(requestId, connectionIdState), ct);

                        streamSessionState.TryComplete();
                        streamSessionState.Dispose();
                    }, (streamSession, remoteCancelEnabled, WebSocket.State, _sender, request.Id, connectionId,
                        cancellationToken)
                    , cancellationToken);
            }

            await _sender.SendMessageAsync(request, cancellationToken);

            return streamSession;
        }

        /// <inheritdoc/>
        public IIngestSession<T> GetIngestSession<T>(
            IOperationRequest operationRequest,
            bool remoteCancelEnabled,
            IOperationOptions? operationOptions = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Throw.If(_disposedPass.WasAcquired, static ()
                => new HubconGenericException("The HubconWebSocket has been disposed."));

            Throw.If(_disconnectionPass.WasAcquired, static ()
                => new HubconGenericException(
                    "The HubconWebSocket has already been disconnected and must be disposed."));

            Throw.IfNot(_connectionPass.WasAcquired, "The connection has not been established.");

            Throw.IfNotEqual(WebSocket?.State, WebSocketState.Open,
                "The HubconWebSocket is not open, it is in a closed or faulted state and must be disposed.");

            var localCts = new CancellationTokenSource();
            var reg1 = cancellationToken.Register(s => ((CancellationTokenSource)s!).Cancel(), localCts);
            var reg2 = _cts.Token.Register(s => ((CancellationTokenSource)s!).Cancel(), localCts);

            var ingestSession = _receiver.Router.CreateIngest<T>(Guid.NewGuid(), connectionId, operationRequest,
                operationOptions!, () =>
                {
                    localCts.Dispose();
                    reg1.Dispose();
                    reg2.Dispose();
                });

            if (remoteCancelEnabled)
            {
                ingestSession.AddCancellation(async () =>
                {
                    if (remoteCancelEnabled && WebSocket.State == WebSocketState.Open)
                        await _sender.SendMessageAsync(new CancelMessage(ingestSession.Id, connectionId),
                            localCts.Token);

                    ingestSession.Dispose();
                }, cancellationToken);
            }

            return ingestSession;
        }

        private static readonly SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, 1);

        /// <inheritdoc/>
        public async ValueTask ConnectAsync(Uri uri, CancellationToken cancellationToken = default)
        {
            try
            {
                await _semaphoreSlim.WaitAsync(CancellationToken.None);

                cancellationToken.ThrowIfCancellationRequested();

                Throw.If(_disposedPass.WasAcquired, static ()
                    => new HubconGenericException("The HubconWebSocket has been disposed."));

                Throw.If(_disconnectionPass.WasAcquired, static ()
                    => new HubconGenericException(
                        "The HubconWebSocket has already been disconnected and must be disposed."));

                Throw.IfNot(_connectionPass.TryAcquirePass(), static ()
                    => new HubconGenericException("The hubcon websocket can only be connected once."));

                if (_loggingEnabled)
                    _logger?.LogInformation("Trying to connect to the server...");

                await WebSocket.ConnectAsync(uri, cancellationToken);

                _receiver.Start();

                if (_loggingEnabled)
                    _logger?.LogInformation("Connected, attempting handshake...");

                var msgId = Guid.NewGuid();
                using var connectionResponse = await SendAndReceiveAsync(
                    new ConnectionInitMessage(msgId),
                    false,
                    TimeSpan.FromMinutes(2),
                    cancellationToken);

                Throw.If(connectionResponse == null, static ()
                    => new HubconRemoteException("Handshake failed: No response received or the request timed out."));

                Throw.If(connectionResponse?.Type != MessageType.connection_ack, static ()
                    => new HubconRemoteException(
                        "Handshake failed: The received message is not a connection ack message."));

                Throw.If(connectionResponse?.Id != msgId, static ()
                    => new HubconRemoteException("Handshake failed: Message ID mismatch."));

                using var ackMessage = new ConnectionAckMessage(connectionResponse!);

                Throw.If(string.IsNullOrWhiteSpace(ackMessage.ConnectionId), (ackMessage, connectionResponse), static x
                    => new HubconRemoteException(
                        $"Handshake failed, connection ID mismatch: Expected ID {x.ackMessage.Id} and received ID {x.connectionResponse?.Id} "));

                Throw.If(ackMessage.Type == MessageType.error, ackMessage, static x
                    => new HubconRemoteException($"Handshake returned an error: {x?.Error}"));

                connectionId = ackMessage.ConnectionId;

                if (_loggingEnabled)
                    _logger?.LogInformation("Connection established.");

                await _context.InterceptorManager.CallInterceptor(InterceptorType.OnConnected, _cts.Token);
            }
            catch
            {
                // Intentional throw
                throw;
            }
            finally
            {
                _semaphoreSlim.Release();
            }
        }

        public async Task DisconnectAsync()
        {
            try
            {
                await _semaphoreSlim.WaitAsync(CancellationToken.None);

                Throw.If(_disposedPass.WasAcquired, static ()
                    => new HubconGenericException("This object has been disposed."));

                Throw.IfNot(_connectionPass.WasAcquired, "The connection has not been established.");

                if (WebSocket.State != WebSocketState.Open && WebSocket.State != WebSocketState.CloseReceived &&
                    _disconnectionPass.TryAcquirePass()) return;

                await WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
            }
            catch
            {
                // Intentional throw
                throw;
            }
            finally
            {
                _semaphoreSlim.Release();
            }
        }

        private static readonly Action<object?> CancelCtsDelegate =
            static state => ((CancellationTokenSource)state!).Cancel();

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            if (!_disposedPass.TryAcquirePass()) return;

            await _sender.DisposeAsync();
            await _receiver.DisposeAsync();

            _cts.Dispose();
            WebSocket.Dispose();

            GC.SuppressFinalize(this);
        }
    }
}