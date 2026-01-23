using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Shared.Core.Websockets.Models;
using System.Buffers;
using System.Net.WebSockets;

namespace Hubcon.Server.Core.Websockets.Helpers
{
    internal sealed class WebSocketMessageReceiver(WebSocket socket, IInternalServerOptions options)
    {
        private readonly WebSocket _socket = socket;
        private readonly int _maxMessageSize = options.MaxWebSocketMessageSize;

        public async Task<TrimmedMemoryOwner?> ReceiveAsync(CancellationToken cancellationToken = default)
        {
            IMemoryOwner<byte> firstPart = MemoryPool<byte>.Shared.Rent(4096);

            try
            {
                var result = await _socket.ReceiveAsync(firstPart.Memory, cancellationToken);

                if ((result.MessageType != WebSocketMessageType.Binary) | (result.Count > _maxMessageSize))
                {
                    firstPart.Dispose();
                    if (result.MessageType != WebSocketMessageType.Binary) _socket.Abort();
                    return null;
                }

                if (result.EndOfMessage)
                {
                    return new TrimmedMemoryOwner(firstPart, result.Count);
                }

                return await ReceiveMultipartAsync(firstPart, result.Count, cancellationToken);
            }
            catch
            {
                firstPart.Dispose();
                throw;
            }
        }

        private async Task<TrimmedMemoryOwner?> ReceiveMultipartAsync(IMemoryOwner<byte> firstPart, int firstCount, CancellationToken ct)
        {
            var parts = new List<IMemoryOwner<byte>>(4) { firstPart };
            int totalBytes = firstCount;

            try
            {
                ValueWebSocketReceiveResult result;
                do
                {
                    var part = MemoryPool<byte>.Shared.Rent(4096);
                    result = await _socket.ReceiveAsync(part.Memory, ct);

                    if ((result.MessageType != WebSocketMessageType.Binary) | (totalBytes + result.Count > _maxMessageSize))
                    {
                        part.Dispose();
                        goto Cleanup;
                    }

                    totalBytes += result.Count;
                    parts.Add(part);
                }
                while (!result.EndOfMessage);

                // Llamamos a un método síncrono para la copia pesada
                // Esto permite usar Spans y es mucho más rápido para el CPU
                return ConsolidateParts(parts, totalBytes);
            }
            catch { goto Cleanup; }

Cleanup:
            foreach (var p in parts) p.Dispose();
            return null;
        }

        // Este método es síncrono, por lo que permite el uso de Spans (ref structs)
        private TrimmedMemoryOwner ConsolidateParts(List<IMemoryOwner<byte>> parts, int totalBytes)
        {
            var finalOwner = MemoryPool<byte>.Shared.Rent(totalBytes);
            var finalSpan = finalOwner.Memory.Span; // Ahora sí podés usar Span
            int offset = 0;

            foreach (var p in parts)
            {
                int toCopy = Math.Min(p.Memory.Length, totalBytes - offset);
                p.Memory.Span.Slice(0, toCopy).CopyTo(finalSpan.Slice(offset));
                offset += p.Memory.Length; // Usamos el largo original para el offset
                p.Dispose();
            }

            return new TrimmedMemoryOwner(finalOwner, totalBytes);
        }
    }
}