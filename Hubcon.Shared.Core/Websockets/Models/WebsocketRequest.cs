using System.Text.Json;

namespace Hubcon.Shared.Core.Websockets.Models
{
    public record class WebsocketRequest(string Id, JsonElement Payload);
}
