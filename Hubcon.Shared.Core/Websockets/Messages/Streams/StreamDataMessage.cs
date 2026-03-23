#pragma warning disable CS1591
using Hubcon.Shared.Core.Websockets.Messages.Generic;
using Hubcon.Shared.Core.Websockets.Models;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hubcon.Shared.Core.Websockets.Messages.Streams
{
    public class StreamDataMessage : BaseMessage
    {
        private JsonElement? _data;

        public StreamDataMessage(BaseMessage baseMessage) : base(baseMessage)
        {
            
        }
        public StreamDataMessage(TrimmedMemoryOwner buffer, Guid? id = null, MessageType? type = null) : base(buffer, id, type)
        {
        }

        [JsonConstructor]
        public StreamDataMessage(Guid id, JsonElement data, string? error = null) : base(MessageType.stream_data, id, error)
        {
            _data = data;
        }

        public JsonElement Data => _data ??= Extract<JsonElement>("data");
    }
}
