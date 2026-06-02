using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Client.Core.Transports.Websockets.Sessions;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Core.Extensions;
using Hubcon.Shared.Core.Tools;
using Hubcon.Shared.Core.Websockets;
using Hubcon.Shared.Core.Websockets.Events;
using Hubcon.Shared.Core.Websockets.Heartbeat;
using Hubcon.Shared.Core.Websockets.Interfaces;
using Hubcon.Shared.Core.Websockets.Messages.Generic;
using Hubcon.Shared.Core.Websockets.Messages.Ingest;
using Hubcon.Shared.Core.Websockets.Messages.Ping;
using Hubcon.Shared.Core.Websockets.Messages.Streams;
using Hubcon.Shared.Core.Websockets.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Hubcon.Client.Core.Transports.Websockets.MessageHandlers
{
    /// <summary>
    /// Reads messages from the provided channel and routes them to the waiting request.
    /// </summary>
    public sealed class MessageRouter : IMessageRouter, IAsyncDisposable
    {
        private volatile int _disposed;

        /// <summary>
        /// An event that's raised when the message router enters in an error state.
        /// </summary>
        public event EventHandler<Exception>? OnError;

        /// <summary>
        /// An event that's raised when the message router receives a pong message.
        /// </summary>
        public event EventHandler<PongMessage>? OnPongMessage;

        private readonly IHubconWebSocket _webSocketClient;
        private readonly Channel<TrimmedMemoryOwner> _receiveChannel;
        private readonly TaskCompletionSource<bool> _startSignal;
        private readonly TaskCompletionSource<bool> _messageRouterDisposed;

        private readonly ConcurrentDictionary<Guid, TaskCompletionSource<BaseMessage>> _requestsTcs =
            new ConcurrentDictionary<Guid, TaskCompletionSource<BaseMessage>>();

        private readonly ConcurrentDictionary<Guid, StreamSession> _streams = new ConcurrentDictionary<Guid, StreamSession>();
        private readonly ConcurrentDictionary<Guid, IngestSession> _ingests = new ConcurrentDictionary<Guid, IngestSession>();

        private ClientWebSocket? _webSocket;

        private readonly Task _routingTask;
        private readonly CancellationTokenSource _cts;
        private readonly TransportContext _context;
        private readonly IDynamicConverter _converter;
        private readonly ILogger<MessageRouter>? _logger;

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="webSocketClient"></param>
        /// <param name="receiveChannel"></param>
        /// <param name="context"></param>
        public MessageRouter(IHubconWebSocket webSocketClient, Channel<TrimmedMemoryOwner> receiveChannel, TransportContext context)
        {
            _webSocketClient = webSocketClient;
            _receiveChannel = receiveChannel;
            _startSignal = new TaskCompletionSource<bool>();
            _messageRouterDisposed = new TaskCompletionSource<bool>();
            _cts = new CancellationTokenSource();
            _webSocket = _webSocketClient.WebSocket;
            _context = context;
            _converter = context.Converter;
            _logger = context.ProxyServiceProvider.GetService<ILogger<MessageRouter>>();

            _routingTask = Task.Factory.StartNew(
                RoutingLoopAsync,
                _cts.Token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        /// <summary>
        /// Starts the routing operations.
        /// </summary>
        public void Start()
        {
            _startSignal.TrySetResult(true);
        }

        /// <summary>
        /// Starts a request with the given ID.
        /// </summary>
        public void BeginRequest(Guid id)
        {
            _requestsTcs.GetOrAdd(id, _ => new TaskCompletionSource<BaseMessage>());
        }

        /// <summary>
        /// Ends a request with the given ID.
        /// </summary>
        /// <param name="id"></param>
        public void EndRequest(Guid id)
        {
            _requestsTcs.TryRemove(id, out _);
        }

        /// <summary>
        /// Waits for a response from the
        /// </summary>
        /// <param name="id"></param>
        /// <param name="timeout"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<BaseMessage?> GetResponseAsync(Guid id, TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (_requestsTcs.TryGetValue(id, out var value))
            {
                return await TimeoutHelper.WaitWithTimeoutAsync(value.Task, timeout, cancellationToken);
            }

            return null;
        }

        public IStreamSession<T> CreateStream<T>(Guid id, string connectionId, IOperationRequest request, Action? onFinishedCallback = null)
        {
            var payload = new StreamInitMessage(id, connectionId, _converter.SerializeToElement(request));
            return (_streams.GetOrAdd(payload.Id, _ => new StreamSession<T>(payload, _context, () =>
            {
                _streams.TryRemove(payload.Id, out var _);
                onFinishedCallback?.Invoke();
            })) as StreamSession<T>)!;
        }

        public IIngestSession<T> CreateIngest<T>(
            Guid id,
            string connectionId,
            IOperationRequest request,
            IOperationOptions operationOptions,
            Action? onFinishedCallback = null)
        {
            var payload = new StreamInitMessage(id, connectionId, _converter.SerializeToElement(request));
            return (_ingests.GetOrAdd(payload.Id, _ => new IngestSession<T>(_webSocketClient, connectionId, _context, request, operationOptions, () =>
            {
                _ingests.TryRemove(payload.Id, out var _);
                onFinishedCallback?.Invoke();
            })) as IngestSession<T>)!;
        }

        private async Task RoutingLoopAsync()
        {
            if (!await _startSignal.Task)
            {
                _messageRouterDisposed.TrySetResult(true);
                return;
            }

            try
            {
                while (await _receiveChannel.Reader.WaitToReadAsync())
                {
                    while (_receiveChannel.Reader.TryRead(out var tmo))
                    {
                        try
                        {
                            Throw.IfNotEqual(_webSocket?.State, WebSocketState.Open);
                            var message = new BaseMessage(tmo);

                            switch (message.Type)
                            {
                                case MessageType.connection_ack:
                                    if (_requestsTcs.TryGetValue(message.Id, out var ackTcs))
                                    {
                                        ackTcs.TrySetResult(message);
                                    }
                                    break;
                                case MessageType.pong:
                                    if (!_context.ClientOptions.WebsocketRequiresPong)
                                        break;

                                    var pongMessage = new PongMessage(message);
                                    OnPongMessage?.Invoke(this, pongMessage);
                                    await _context.InterceptorManager.CallInterceptor(InterceptorType.OnPong);
                                    break;

                                case MessageType.error:
                                    if (message?.Id != null && _requestsTcs.TryGetValue(message.Id, out var subToError))
                                    {
                                        subToError.TrySetResult(message);
                                    }

                                    break;

                                case MessageType.stream_data:
                                    var streamData = new StreamDataMessage(message);
                                    if (streamData?.Id != null && _streams.TryGetValue(streamData.Id, out var stream))
                                    {
                                        stream.Next(streamData.Data);
                                    }

                                    break;

                                case MessageType.stream_complete:
                                    var streamComplete = new StreamCompleteMessage(message);

                                    if (streamComplete?.Id != null &&
                                        _streams.TryGetValue(streamComplete.Id, out var streamCompleteInfo))
                                    {
                                        streamCompleteInfo.TryComplete();
                                        streamCompleteInfo.Dispose();
                                    }

                                    break;

                                case MessageType.ingest_init_ack:
                                    if (_requestsTcs.TryGetValue(message.Id, out var ingestInitAckTcs))
                                    {
                                        ingestInitAckTcs.TrySetResult(message);
                                    }

                                    break;

                                case MessageType.ingest_result:
                                    if (_requestsTcs.TryGetValue(message.Id, out var ingestResultMessageTcs))
                                    {
                                        ingestResultMessageTcs.TrySetResult(message);
                                    }

                                    break;

                                case MessageType.token_update:
                                    if (_requestsTcs.TryGetValue(message.Id, out var tokenUpdateResponseTcs))
                                    {
                                        tokenUpdateResponseTcs.TrySetResult(message);
                                    }

                                    break;

                                case MessageType.ingest_data_ack:
                                    if (_requestsTcs.TryGetValue(message.Id, out var ingestDataAckTcs))
                                    {
                                        ingestDataAckTcs.TrySetResult(message);
                                    }

                                    break;

                                case MessageType.operation_response:
                                    if (_requestsTcs.TryGetValue(message.Id, out var ormTcs))
                                    {
                                        ormTcs.TrySetResult(message);
                                    }

                                    break;

                                default:
                                    var msg = $"Unsupported message type. Received type: {message.Type}";
                                    OnError?.Invoke(this, new HubconGenericException(msg));

                                    if (_context.ClientOptions.LoggingEnabled)
                                        _logger?.LogError(msg);

                                    break;
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            if (_context.ClientOptions.LoggingEnabled)
                                _logger?.LogError("Message router error: {0}", ex.Message);

                            OnError?.Invoke(this, new HubconGenericException(ex.Message));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (_context.ClientOptions.LoggingEnabled)
                    _logger?.LogError("Message router error: {0}", ex.Message);

                OnError?.Invoke(this, new HubconGenericException(ex.Message));
            }
            finally
            {
                if (_context.ClientOptions.LoggingEnabled)
                    _logger?.LogError("Message router finished.");

                _messageRouterDisposed.TrySetResult(true);
            }
        }

        /// <summary>
        /// Disposes this object and its resources.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 1)
            {
                return;
            }

            _cts.Cancel();
            _receiveChannel.Writer.TryComplete();
            _startSignal.TrySetResult(false);
            await _messageRouterDisposed.Task;
            _routingTask.Dispose();
            _webSocket = null;

            foreach (var request in _requestsTcs)
                request.Value.TrySetCanceled();

            _requestsTcs.Clear();

            foreach (var stream in _streams.Values)
                stream.Dispose();

            _streams.Clear();

            foreach (var ingest in _ingests.Values)
                ingest.Dispose();

            _ingests.Clear();

            GC.SuppressFinalize(this);
        }
    }
}