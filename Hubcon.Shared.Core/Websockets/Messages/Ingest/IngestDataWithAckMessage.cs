using Hubcon.Shared.Core.Websockets.Messages.Generic;
using Hubcon.Shared.Core.Websockets.Models;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hubcon.Shared.Core.Websockets.Messages.Ingest
{
    public class IngestDataWithAckMessage : BaseMessage
    {
        private JsonElement? _data;

        public IngestDataWithAckMessage(BaseMessage baseMessage) : base(baseMessage)
        {
            
        }
        public IngestDataWithAckMessage(TrimmedMemoryOwner buffer, Guid? id = null, MessageType? type = null) : base(buffer, id, type)
        {
        }

        [JsonConstructor]
        public IngestDataWithAckMessage(Guid id, JsonElement data, string? error = null) : base(MessageType.ingest_data_with_ack, id, error)
        {
            _data = data;
        }

        [JsonPropertyName("data")]
        public JsonElement Data => _data ??= Extract<JsonElement>("data");
    }
}
