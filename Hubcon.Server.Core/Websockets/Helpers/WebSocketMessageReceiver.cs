using Hubcon.Server.Core.Configuration;
using Microsoft.Extensions.Options;
using System.Net.WebSockets;
using System.Text;

namespace Hubcon.Server.Core.Websockets.Helpers
{
    public class WebSocketMessageReceiver(WebSocket socket, IInternalServerOptions options)
    {
        private readonly WebSocket _socket = socket;
        private readonly byte[] _buffer = new byte[options.MaxWebSocketMessageSize];

        public async Task<string?> ReceiveAsync()
        {           
            var sb = new StringBuilder();
            WebSocketReceiveResult result;

            do
            {
                result = await _socket.ReceiveAsync(new ArraySegment<byte>(_buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close) return null;

                sb.Append(Encoding.UTF8.GetString(_buffer, 0, result.Count));
            } while (!result.EndOfMessage);

            return sb.ToString();
        }
    }
}