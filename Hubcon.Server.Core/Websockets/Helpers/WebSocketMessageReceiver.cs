using Hubcon.Server.Core.Configuration;
using Hubcon.Shared.Core.Websockets.Models;
using Microsoft.Extensions.Options;
using System.Buffers;
using System.Net.WebSockets;
using System.Text;

namespace Hubcon.Server.Core.Websockets.Helpers
{
    internal sealed class WebSocketMessageReceiver(WebSocket socket, IInternalServerOptions options)
    {
        private readonly WebSocket _socket = socket;
        private readonly int _maxMessageSize = options.MaxWebSocketMessageSize;

        public async Task<TrimmedMemoryOwner?> ReceiveAsync(CancellationToken cancellationToken = default)
        {
            var parts = new List<IMemoryOwner<byte>>();
            int totalBytes = 0;

            try
            {
                ValueWebSocketReceiveResult result;
                do
                {
                    var part = MemoryPool<byte>.Shared.Rent(4096);
                    var segment = part.Memory;

                    result = await _socket.ReceiveAsync(segment, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();

                    if (result.MessageType == WebSocketMessageType.Close)
                        return null;

                    if (result.Count < segment.Length)
                        part = new TrimmedMemoryOwner(part, result.Count);

                    totalBytes += result.Count;
                    parts.Add(part);
                }
                while (!result.EndOfMessage);

                var finalOwner = MemoryPool<byte>.Shared.Rent(totalBytes);
                var finalMemory = finalOwner.Memory.Slice(0, totalBytes);
                int offset = 0;

                foreach (var part in parts)
                {
                    part.Memory.Span.CopyTo(finalMemory.Span.Slice(offset));
                    offset += part.Memory.Length;
                    part.Dispose();
                }

                return new TrimmedMemoryOwner(finalOwner, totalBytes);
            }
            catch
            {
                foreach (var part in parts)
                    part.Dispose();

                return null;
            }
        }
    }
}