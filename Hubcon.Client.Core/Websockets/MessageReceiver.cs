using System;
using System.Buffers;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Shared.Core.Extensions;
using Hubcon.Shared.Core.Tools;
using Hubcon.Shared.Core.Websockets.Messages.Generic;
using Hubcon.Shared.Core.Websockets.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hubcon.Client.Core.Websockets
{
    /// <summary>
    /// Manages the messages received through the provided websocket connection.
    /// </summary>
    public class MessageReceiver : IMessageReceiver, IAsyncDisposable
    {
        /// <summary>
        /// An event that's raised when the reception loop produces an error.
        /// </summary>
        public event EventHandler<Exception>? OnError;
        
        /// <summary>
        /// An event that's raised when the websocket receives a close message from the server.
        /// </summary>
        public event EventHandler? OnCloseReceived;

        private volatile int _disposed;
        private readonly TaskCompletionSource<bool> _receiveLoopDisposed;
        private readonly TaskCompletionSource<bool> _startSignal;

        private readonly Channel<TrimmedMemoryOwner> _receiveChannel;
        private readonly TransportContext _context;
        private readonly CancellationTokenSource _cts;
        private readonly ILogger<MessageReceiver>? _logger;
        private readonly Task _receiveTask;
        private readonly MessageRouter _router; 
        
        private ClientWebSocket? _webSocket;
        
        
        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="webSocket">The current websocket client.</param>
        /// <param name="context">The context of the transport.</param>
        public MessageReceiver(ClientWebSocket webSocket, TransportContext context)
        {
            _cts = new CancellationTokenSource();
            _logger = context.ProxyServiceProvider.GetService<ILogger<MessageReceiver>>();
            _context = context;
            _webSocket = webSocket;
            _startSignal = new TaskCompletionSource<bool>();

            _receiveLoopDisposed = new TaskCompletionSource<bool>();

            _receiveChannel = Channel.CreateBounded<TrimmedMemoryOwner>(
                new BoundedChannelOptions(20000 * context.ClientOptions.MessageProcessorsCount)
                {
                    FullMode = BoundedChannelFullMode.Wait,
                    SingleWriter = true,
                    SingleReader = true
                });

            _router = new MessageRouter(_webSocket, _receiveChannel, context);

            _receiveTask = Task.Factory.StartNew(
                ReceiveLoopAsync,
                _cts.Token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        /// <summary>
        /// The message router manages operation parsing and coordination.
        /// </summary>
        public MessageRouter Router => _router;

        /// <summary>
        /// Starts the reception loop operations.
        /// </summary>
        public void Start()
        {
            _startSignal.TrySetResult(true);
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
                                OnCloseReceived?.Invoke(this, null);
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
                    _logger?.LogInformation("ReceiveLoop finished.");
                }
                
                _receiveLoopDisposed.TrySetResult(true);
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
            await _receiveLoopDisposed.Task;
            _receiveTask.Dispose();
            
            _webSocket = null;
            
            GC.SuppressFinalize(this);
        }
    }
}