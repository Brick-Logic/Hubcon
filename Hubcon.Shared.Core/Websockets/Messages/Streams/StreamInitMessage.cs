#pragma warning disable CS1591
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

        public StreamInitMessage(BaseMessage baseMessage) : base(baseMessage)
        {
            
        }
        public StreamInitMessage(TrimmedMemoryOwner buffer, Guid? id = null, string? connectionId = null, MessageType? type = null) : base(buffer, id, connectionId, type)
        {
        }

        [JsonConstructor]
        public StreamInitMessage(Guid id, string connectionId, JsonElement payload, string? error = null) : base(MessageType.stream_init, id, connectionId, error)
        {
            _payload = payload;
        }

        public JsonElement Payload => _payload ??= Extract<JsonElement>("payload");
    }
}
