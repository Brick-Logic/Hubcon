#pragma warning disable CS1591
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

        public IngestInitMessage(BaseMessage baseMessage) : base(baseMessage)
        {
            
        }
        public IngestInitMessage(TrimmedMemoryOwner buffer, Guid? id = null, string? connectionId = null, MessageType? type = null) : base(buffer, id, connectionId, type)
        {
        }

        [JsonConstructor]
        public IngestInitMessage(Guid id, string connectionId, Guid[] streamIds, JsonElement payload, string? error = null) : base(MessageType.ingest_init, id, connectionId, error)
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
