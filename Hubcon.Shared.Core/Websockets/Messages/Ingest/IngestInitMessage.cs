using Hubcon.Shared.Core.Websockets.Messages.Generic;
using Hubcon.Shared.Core.Websockets.Models;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hubcon.Shared.Core.Websockets.Messages.Ingest
{
    public class IngestInitMessage : BaseMessage
    {
        private Guid[]? _streamIds;
        private JsonElement? _payload;

        public IngestInitMessage(TrimmedMemoryOwner buffer, Guid? id = null, MessageType? type = null) : base(buffer, id, type)
        {
        }

        [JsonConstructor]
        public IngestInitMessage(Guid id, Guid[] streamIds, JsonElement payload) : base(MessageType.ingest_init, id)
        {
            _streamIds = streamIds;
            _payload = payload;
        }

        [JsonPropertyName("streamIds")]
        public Guid[] StreamIds => _streamIds ??= Extract<Guid[]>("streamIds")!;

        [JsonPropertyName("payload")]
        public JsonElement Payload => _payload ??= Extract<JsonElement>("payload")!;
    }
}
