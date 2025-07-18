using Hubcon.Shared.Abstractions.Interfaces;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Hubcon.Server.Core.Websockets.Helpers
{
    internal sealed class WebSocketMessageSender(WebSocket _webSocket, IDynamicConverter converter)
    {
        public async Task SendAsync<T>(T message)
        {
            var json = converter.Serialize(message);
            var bytes = Encoding.UTF8.GetBytes(json);
            await _webSocket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}