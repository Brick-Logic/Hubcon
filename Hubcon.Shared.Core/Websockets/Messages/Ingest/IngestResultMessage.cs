#pragma warning disable CS1591
using Hubcon.Shared.Core.Websockets.Messages.Generic;
using Hubcon.Shared.Core.Websockets.Models;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hubcon.Shared.Core.Websockets.Messages.Ingest
{
    public sealed class IngestResultMessage : BaseMessage
    {
        private JsonElement? _data;

        public IngestResultMessage(BaseMessage baseMessage) : base(baseMessage)
        {
            
        }

        public IngestResultMessage(TrimmedMemoryOwner buffer, Guid? id = null, MessageType? type = null) : base(buffer, id, type)
        {
        }

        [JsonConstructor]
        public IngestResultMessage(Guid id, JsonElement data, string? error = null) : base(MessageType.ingest_result, id, error)
        {
            _data = data;
        }

        [JsonPropertyName("data")]
        public JsonElement Data => _data ??= Extract<JsonElement>("data");
    }
}
