#pragma warning disable CS1591
using Hubcon.Shared.Core.Websockets.Messages.Generic;
using Hubcon.Shared.Core.Websockets.Models;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hubcon.Shared.Core.Websockets.Messages.Streams
{
    public class StreamDataWithAckMessage : BaseMessage
    {
        private JsonElement? _data;
        private Guid? _ackId;

        public StreamDataWithAckMessage(BaseMessage baseMessage) : base(baseMessage)
        {
            
        }
        public StreamDataWithAckMessage(TrimmedMemoryOwner buffer, Guid? id = null, MessageType? type = null) : base(buffer, id, type)
        {
        }

        [JsonConstructor]
        public StreamDataWithAckMessage(Guid id, JsonElement data, Guid ackId, string? error = null) : base(MessageType.stream_data_with_ack, id, error)
        {
            _data = data;
            _ackId = ackId;
        }

        public JsonElement Data => _data ??= Extract<JsonElement>("data");
        public Guid AckId => _ackId ??= Extract<Guid>("ackId");
    }
}
