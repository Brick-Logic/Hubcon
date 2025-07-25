using Hubcon.Shared.Core.Websockets.Messages.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hubcon.Shared.Core.Websockets.Messages.Streams
{
    public record class StreamInitMessage : BaseMessage
    {
        private JsonElement? _payload;

        public StreamInitMessage(ReadOnlyMemory<byte> buffer) : base(buffer)
        {
        }

        [JsonConstructor]
        public StreamInitMessage(Guid id, JsonElement payload) : base(MessageType.stream_init, id)
        {
            _payload = payload;
        }

        public JsonElement Payload => _payload ??= Extract<JsonElement>("payload");
    }
}
