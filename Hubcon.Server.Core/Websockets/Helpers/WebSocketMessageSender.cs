using Hubcon.Shared.Abstractions.Interfaces;
using System.Net.WebSockets;
using System.Text;

namespace Hubcon.Server.Core.Websockets.Helpers
{
    public class WebSocketMessageSender(WebSocket _webSocket, IDynamicConverter converter)
    {
        public async Task SendAsync<T>(T message)
        {
            var serialized = converter.Serialize(message);

            await _webSocket!.SendAsync(
                Encoding.UTF8.GetBytes(serialized),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None
            );
        }
    }
}