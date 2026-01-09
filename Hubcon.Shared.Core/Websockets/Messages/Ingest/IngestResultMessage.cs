using Hubcon.Shared.Core.Websockets.Messages.Generic;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hubcon.Shared.Core.Websockets.Messages.Ingest
{
    public sealed class IngestResultMessage : BaseMessage
    {
        private JsonElement? _data;

        public IngestResultMessage(ReadOnlyMemory<byte> buffer, Guid? id = null, MessageType? type = null) : base(buffer, id, type)
        {
        }

        [JsonConstructor]
        public IngestResultMessage(Guid id, JsonElement data) : base(MessageType.ingest_result, id)
        {
            _data = data;
        }

        [JsonPropertyName("data")]
        public JsonElement Data => _data ??= Extract<JsonElement>("data");
    }
}
