using Hubcon.Shared.Core.Websockets.Messages.Generic;
using Hubcon.Shared.Core.Websockets.Models;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;


namespace Hubcon.Shared.Core.Websockets.Messages.Streams
{
    public class StreamInitMessage : BaseMessage
    {
        private JsonElement? _payload;

        public StreamInitMessage(TrimmedMemoryOwner buffer, Guid? id = null, MessageType? type = null) : base(buffer, id, type)
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
