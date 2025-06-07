using System.Text.Json;

namespace Hubcon.Websockets.Shared.Models
{
    public record class WebsocketRequest(string Id, JsonElement Payload);
}
