using Hubcon.Shared.Core.Websockets;
using Hubcon.Shared.Core.Websockets.Messages.Generic;
using System.Text.Json;

namespace Hubcon.Shared.Core.Websockets.Messages.Subscriptions
{
    public record class StreamInitMessage(string StreamId, JsonElement Payload) : BaseMessage(MessageType.stream_init);
}
