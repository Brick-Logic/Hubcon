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

namespace Hubcon.Client.Core.Websockets
{
    /// <summary>
    /// Manages 
    /// </summary>
    public sealed class HubconWebSocket : IWebSocketClient
    {
        private volatile bool _disposed = false;

        private readonly CancellationTokenSource _cts;
        
        private readonly GenericObservable<PongMessage> _pongStream;
        private readonly GenericObservable<Exception> _errorStream;
        private readonly SemaphoreSlim _reconnectLock = new SemaphoreSlim(1, 1);
        
        private readonly TransportContext _context;
        private readonly ILogger<HubconWebSocket>? _logger;
        private readonly IClientOptions _options;
        private readonly IDynamicConverter _converter;
        private readonly IClientOptions _clientOptions;
        private readonly IServiceProvider _serviceProvider;
        
        private readonly ClientWebSocket _webSocket;
        private readonly MessageSender _sender;
        private readonly MessageReceiver _receiver;
        private readonly string connectionId;
        private readonly Uri _uri;

        public HubconWebSocket(Uri uri, TransportContext context)
        {
            _cts = new CancellationTokenSource();
            _uri = uri;
            _context = context;
            
            _webSocket = new ClientWebSocket();
            
            _options = context.ClientOptions;
            _converter = context.Converter;
            _clientOptions = context.ClientOptions;
            _serviceProvider = context.ProxyServiceProvider;
            _logger = context.ProxyServiceProvider.GetService<ILogger<HubconWebSocket>>();
            connectionId = Guid.NewGuid().ToString();

            _receiver = new MessageReceiver(_webSocket, context);
            _sender = new MessageSender(connectionId, _webSocket, context);
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
        /// <param name="cancellationToken"></param>
        /// <typeparam name="TRequest"></typeparam>
        /// <returns></returns>
        public async Task<BaseMessage?> SendAndReceive<TRequest>(TRequest message, CancellationToken cancellationToken = default) where TRequest : BaseMessage
        {
            Throw.IfNotEqual(_webSocket?.State, WebSocketState.Open, "WebSocket is not open.");

            try
            {
                _receiver.Router.BeginRequest(message.Id);
                await _sender.SendMessageAsync(message, cancellationToken);
                return await _receiver.Router.GetResponseAsync(message.Id, _clientOptions.WebsocketTimeout, cancellationToken);
            }
            finally
            {
                _receiver.Router.EndRequest(message.Id);
            }
        }
        
        public async Task<StreamSession<T>> Stream<T>(IOperationRequest payload, bool remoteCancelEnabled, CancellationToken cancellationToken = default)
        {
            Throw.IfNotEqual(_webSocket?.State, WebSocketState.Open, "WebSocket is not open.");

            using var request = new StreamInitMessage(Guid.NewGuid(), connectionId, _converter.SerializeToElement(payload));

            var streamSession = _receiver.Router.CreateStream<T>(request);

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
        
        public async Task<T> IngestMultiple<T>(
            IOperationRequest operationRequest,
            bool remoteCancelEnabled,
            IOperationOptions? operationOptions = null,
            CancellationToken cancellationToken = default)
        {
            Throw.IfNotEqual(_webSocket?.State, WebSocketState.Open, "WebSocket is not open.");
            
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
                if (!cts.IsCancellationRequested)
                {
                    var msg = new IngestCompleteMessage(initialAckId, connectionId, sources.Keys.ToArray());
                    await SendMessageAsync(msg);
                }

                _ingests.TryRemove(initialAckId, out var removedIngest);
                removedIngest.Item1?.TrySetCanceled();
                removedIngest.Item2?.Cancel();
            }
        }
        
        public Task Connect(CancellationToken cancellationToken = default)
        {
            return _webSocket.ConnectAsync(_uri, cancellationToken);
        }
        
        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;

            await _sender.DisposeAsync();
            await _receiver.DisposeAsync();
        }
    }
}