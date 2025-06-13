using Hubcon.Shared.Core.Websockets.Messages.Generic;
using System.Text.Json;

namespace Hubcon.Shared.Core.Websockets.Messages.Streams
{
    public record class StreamInitMessage(string StreamId, JsonElement Payload) : BaseMessage(MessageType.stream_init);
}
