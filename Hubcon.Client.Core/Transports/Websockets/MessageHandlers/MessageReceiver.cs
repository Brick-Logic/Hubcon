using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Shared.Core.Extensions;
using Hubcon.Shared.Core.Tools;
using Hubcon.Shared.Core.Websockets.Messages.Generic;
using Hubcon.Shared.Core.Websockets.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Hubcon.Client.Core.Transports.Websockets.MessageHandlers
{
    /// <summary>
    /// Manages the messages received through the provided websocket connection.
    /// </summary>
    public class MessageReceiver : IMessageReceiver, IAsyncDisposable
    {
        /// <inheritdoc/>
        public event EventHandler<Exception>? OnError;
        
        /// <inheritdoc/>
        public event Action? OnDisconnected;
        
        /// <inheritdoc/>
        public event Action? OnCloseReceived;

        private readonly AtomicPass _isDisposedPass = new();
        private readonly TaskCompletionSource<bool> _receiveLoopDisposed;
        private readonly TaskCompletionSource<bool> _startSignal;

        private readonly Channel<TrimmedMemoryOwner> _receiveChannel;
        private readonly TransportContext _context;
        private readonly IClientOptions _clientOptions;
        private readonly CancellationTokenSource _cts;
        private readonly ILogger<MessageReceiver>? _logger;
        private readonly Task _receiveTask;
        
        private readonly MessageRouter _router; 
        
        private ClientWebSocket? _webSocket;
        
        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="webSocketClient">The current websocket client.</param>
        /// <param name="context">The context of the transport.</param>
        public MessageReceiver(IHubconWebSocket webSocketClient, TransportContext context)
        {
            _cts = new CancellationTokenSource();
            _logger = context.ProxyServiceProvider.GetService<ILogger<MessageReceiver>>();
            _context = context;
            _clientOptions = context.ClientOptions;
            _webSocket = webSocketClient.WebSocket;
            _startSignal = new TaskCompletionSource<bool>();

            _receiveLoopDisposed = new TaskCompletionSource<bool>();

            _receiveChannel = Channel.CreateBounded<TrimmedMemoryOwner>(
                new BoundedChannelOptions(20000 * context.ClientOptions.MessageProcessorsCount)
                {
                    FullMode = BoundedChannelFullMode.Wait,
                    SingleWriter = true,
                    SingleReader = true
                });

            _router = new MessageRouter(webSocketClient, _receiveChannel, context);

            _receiveTask = Task.Factory.StartNew(
                ReceiveLoopAsync,
                _cts.Token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        /// <summary>
        /// The message router manages operation parsing and coordination.
        /// </summary>
        public IMessageRouter Router => _router;

        /// <summary>
        /// Starts the reception loop operations.
        /// </summary>
        public void Start()
        {
            _startSignal.TrySetResult(true);
            _router.Start();
        }

        /// <summary>
        /// Waits for a message given a specific message id. This overload uses the framework's default timeout for websockets.
        /// </summary>
        /// <param name="id">The message ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task that returns a <see cref="BaseMessage"/> object, which contains the server's raw response.</returns>
        public ValueTask<BaseMessage?> Receive(Guid id, CancellationToken cancellationToken = default)
        {
            return _router.GetResponseAsync(id, _clientOptions.WebsocketTimeout ,cancellationToken);
        }

        /// <summary>
        /// Waits for a message given a specific message id. This overload requires a timeout.
        /// </summary>
        /// <param name="id">The message ID.</param>
        /// <param name="timeout">The time to wait for the response.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task that returns a <see cref="BaseMessage"/> object, which contains the server's raw response.</returns>
        public ValueTask<BaseMessage?> Receive(Guid id, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            return _router.GetResponseAsync(id, timeout, cancellationToken);
        }

        private async Task ReceiveLoopAsync()
        {
            if (!await _startSignal.Task)
            {
                return;
            }
            
            if (_context.ClientOptions.LoggingEnabled)
            {
                _logger?.LogInformation("Receive loop started.");
            }
            
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    Throw.IfNull(_webSocket);
                    Throw.IfNotEqual(_webSocket.State, WebSocketState.Open);

                    var parts = new List<IMemoryOwner<byte>>();
                    int totalBytes = 0;

                    ValueWebSocketReceiveResult result;

                    do
                    {
                        var part = MemoryPool<byte>.Shared.Rent(4096);
                        var segment = part.Memory;

                        result = await _webSocket.ReceiveAsync(segment, _cts.Token);
                        
                        if (result.MessageType != WebSocketMessageType.Binary)
                        {
                            if (result.MessageType == WebSocketMessageType.Close)
                            {
                                OnCloseReceived?.Invoke();
                                return;
                            }

                            continue;
                        }

                        if (result.Count < segment.Length)
                            part = new TrimmedMemoryOwner(part, result.Count);

                        totalBytes += result.Count;
                        parts.Add(part);
                    } while (!result.EndOfMessage);

                    var finalOwner = MemoryPool<byte>.Shared.Rent(totalBytes);
                    var finalMemory = finalOwner.Memory.Slice(0, totalBytes);
                    int offset = 0;

                    foreach (var part in parts)
                    {
                        part.Memory.Slice(0).CopyTo(finalMemory.Slice(offset));
                        offset += part.Memory.Length;
                        part.Dispose();
                    }

                    await _receiveChannel.Writer.WriteAsync(new TrimmedMemoryOwner(finalOwner, totalBytes), _cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                if (_context.ClientOptions.LoggingEnabled)
                    _logger?.LogError("Error en ReceiveLoop: {0}", ex.Message);
                
                OnError?.Invoke(this, ex);
            }
            finally
            {
                if (_context.ClientOptions.LoggingEnabled)
                {
                    _logger?.LogInformation("Message receiver loop finished.");
                }
                
                OnDisconnected?.Invoke();
                _receiveLoopDisposed.TrySetResult(true);
            }
        }
        
        /// <summary>
        /// Disposes this object and its resources.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (!_isDisposedPass.TryAcquirePass())
                return;
            
            _cts.Cancel();
            _startSignal.TrySetResult(false);
            if(await _startSignal.Task) await _receiveLoopDisposed.Task;
            _receiveChannel.Writer.TryComplete();
            _receiveTask.Dispose();
            
            _webSocket = null;
            OnDisconnected = null;
            OnError = null;
            
            await _router.DisposeAsync();
            
            if (_context.ClientOptions.LoggingEnabled)
            {
                _logger?.LogInformation("Message receiver finalized.");
            }
            
            GC.SuppressFinalize(this);
        }
    }
}