using Hubcon.Shared.Core.Websockets.Messages.Generic;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hubcon.Shared.Core.Websockets.Messages.Ingest
{
    public class IngestDataMessage : BaseMessage
    {
        private JsonElement? _data;

        public IngestDataMessage(ReadOnlyMemory<byte> buffer, Guid? id = null, MessageType? type = null) : base(buffer, id, type)
        {
        }

        [JsonConstructor]
        public IngestDataMessage(Guid id, JsonElement data) : base(MessageType.ingest_data, id)
        {
            _data = data;
        }

        [JsonPropertyName("data")]
        public JsonElement Data => _data ??= Extract<JsonElement>("data");
    }
}
