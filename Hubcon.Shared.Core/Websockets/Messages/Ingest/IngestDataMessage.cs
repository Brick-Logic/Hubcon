#pragma warning disable CS1591
using Hubcon.Shared.Core.Websockets.Messages.Generic;
using Hubcon.Shared.Core.Websockets.Models;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hubcon.Shared.Core.Websockets.Messages.Ingest
{
    public class IngestDataMessage : BaseMessage
    {
        private JsonElement? _data;

        public IngestDataMessage(BaseMessage baseMessage) : base(baseMessage)
        {
            
        }
        public IngestDataMessage(TrimmedMemoryOwner buffer, Guid? id = null, string? connectionId = null, MessageType? type = null) : base(buffer, id, connectionId, type)
        {
        }

        [JsonConstructor]
        public IngestDataMessage(Guid id, string connectionId, JsonElement data, string? error = null) : base(MessageType.ingest_data, id, connectionId, error)
        {
            _data = data;
        }

        [JsonPropertyName("data")]
        public JsonElement Data => _data ??= Extract<JsonElement>("data");
    }
}
