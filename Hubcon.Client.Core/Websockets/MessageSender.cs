using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Client.Abstractions.Models;
using Hubcon.Shared.Core.Extensions;
using Hubcon.Shared.Core.Websockets.Messages.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hubcon.Client.Core.Websockets
{
    /// <summary>
    /// Manages the messages sent through the provided websocket connection.
    /// </summary>
    public class MessageSender : IMessageSender, IAsyncDisposable
    {
        /// <summary>
        /// An event that's raised when the send loop produces an error.
        /// </summary>
        public event EventHandler<Exception>? OnError;

        private volatile int _disposed;
        private readonly TaskCompletionSource<bool> _sendLoopDisposed;

        private readonly Channel<ByteMessage> _sendChannel;
        private readonly TransportContext _context;
        private readonly string _connectionId;
        private readonly CancellationTokenSource _cts;
        private readonly ILogger<MessageSender>? _logger;
        private readonly Task _sendTask;
        private ClientWebSocket? _webSocket;


        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="webSocketClient">The current websocket client.</param>
        /// <param name="context">The context of the transport.</param>
        /// <param name="connectionId">The current websocket connection id.</param>
        public MessageSender(IWebSocketClient webSocketClient, TransportContext context, string connectionId)
        {
            _cts = new CancellationTokenSource();
            _logger = context.ProxyServiceProvider.GetService<ILogger<MessageSender>>();
            _context = context;
            _webSocket = webSocketClient.WebSocket;
            _connectionId = connectionId;

            _sendLoopDisposed = new TaskCompletionSource<bool>();

            _sendChannel = Channel.CreateBounded<ByteMessage>(new BoundedChannelOptions(20000)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleWriter = false,
                SingleReader = true
            });

            _sendTask = Task.Factory.StartNew(
                SendLoopAsync,
                _cts.Token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        /// <summary>
        /// Allows sending a message through the web socket connection using optimized serialization.
        /// </summary>
        /// <param name="message">The message to send. The message is disposed after usage.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <typeparam name="T"></typeparam>
        public async ValueTask SendMessageAsync<T>(T message, CancellationToken cancellationToken = default)
            where T : BaseMessage
        {
            Throw.IfEqual(_disposed, 1, "The message sender has already been disposed.");
            
            var pipe = new Pipe();
            var writer = new Utf8JsonWriter(pipe.Writer);

            _context.Converter.Serialize(writer, message);

            await writer.FlushAsync(cancellationToken);
            await pipe.Writer.CompleteAsync();

            var result = await pipe.Reader.ReadAsync(cancellationToken);
            var buffer = result.Buffer;

            var bytes = buffer.ToArray();
            await pipe.Reader.CompleteAsync();

            await _sendChannel.Writer.WriteAsync(new ByteMessage(bytes, _connectionId, cancellationToken), cancellationToken);
        }

        private async Task SendLoopAsync()
        {
            try
            {
                if (_context.ClientOptions.LoggingEnabled)
                    _logger?.LogError("Send loop started.");

                while (await _sendChannel.Reader.WaitToReadAsync(_cts.Token))
                {
                    while (_sendChannel.Reader.TryRead(out var buffer))
                    {
                        Throw.IfEqual(_disposed, 1, "Send loop: MessageSender has been disposed.");
                        Throw.IfNotEqual(_webSocket?.State, WebSocketState.Open, "Send loop: WebSocket is closed.");

                        if (buffer.CancellationToken.IsCancellationRequested || buffer.ConnectionId != _connectionId)
                            continue;

                        var segment = new ArraySegment<byte>(buffer.Bytes);
                        await _webSocket!.SendAsync(segment, WebSocketMessageType.Binary, true, _cts.Token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                if (_context.ClientOptions.LoggingEnabled)
                    _logger?.LogError("{0}", ex.Message);

                OnError?.Invoke(this, ex);
            }
            finally
            {
                if (_context.ClientOptions.LoggingEnabled)
                    _logger?.LogError("Send loop finished.");

                _sendLoopDisposed.TrySetResult(true);
            }
        }

        /// <summary>
        /// Disposes this object and its resources.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 1)
                return;

            _cts.Cancel();
            _sendChannel.Writer.TryComplete();
            await _sendLoopDisposed.Task;
            _sendTask.Dispose();
            _webSocket = null;

            GC.SuppressFinalize(this);
        }
    }
}