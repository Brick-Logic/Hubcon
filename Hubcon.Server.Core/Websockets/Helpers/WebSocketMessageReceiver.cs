using Hubcon.Server.Core.Configuration;
using Microsoft.Extensions.Options;
using System.Buffers;
using System.Net.WebSockets;
using System.Text;

namespace Hubcon.Server.Core.Websockets.Helpers
{
    internal sealed class WebSocketMessageReceiver(WebSocket socket, IInternalServerOptions options)
    {
        private readonly WebSocket _socket = socket;
        private readonly byte[] _buffer = new byte[options.MaxWebSocketMessageSize];
        private readonly Decoder _decoder = Encoding.UTF8.GetDecoder();
        private readonly int _charBufferSize = options.MaxWebSocketMessageSize;

        public async Task<string?> ReceiveAsync()
        {
            WebSocketReceiveResult result;
            var charBuffer = ArrayPool<char>.Shared.Rent(_charBufferSize);
            var stringBuilder = new StringBuilder();

            try
            {
                do
                {
                    result = await _socket.ReceiveAsync(new ArraySegment<byte>(_buffer), CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close) return null;

                    int charsDecoded = _decoder.GetChars(
                        _buffer, 0, result.Count,
                        charBuffer, 0,
                        flush: result.EndOfMessage
                    );

                    stringBuilder.Append(charBuffer, 0, charsDecoded);

                } while (!result.EndOfMessage);

                return stringBuilder.ToString();
            }
            finally
            {
                ArrayPool<char>.Shared.Return(charBuffer);
            }
        }
    }
}