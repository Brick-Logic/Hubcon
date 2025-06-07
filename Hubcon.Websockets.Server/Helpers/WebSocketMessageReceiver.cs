using System.Net.WebSockets;
using System.Text;

namespace Hubcon.Websockets.Server.Helpers
{
    public class WebSocketMessageReceiver
    {
        private readonly WebSocket _socket;
        private readonly byte[] _buffer = new byte[8192];

        public WebSocketMessageReceiver(WebSocket socket)
        {
            _socket = socket;
        }

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