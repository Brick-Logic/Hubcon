using System.Net.WebSockets;
using System.Text.Json;
using System.Text;

namespace Hubcon.Server.Core.Websockets.Helpers
{
    public class WebSocketMessageSender
    {
        private readonly WebSocket _socket;

        public WebSocketMessageSender(WebSocket socket)
        {
            _socket = socket;
        }

        public async Task SendAsync<T>(T message)
        {
            var json = JsonSerializer.Serialize(message);
            var bytes = Encoding.UTF8.GetBytes(json);
            await _socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}