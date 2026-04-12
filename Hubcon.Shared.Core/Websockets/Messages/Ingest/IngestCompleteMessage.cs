#pragma warning disable CS1591
using Hubcon.Shared.Core.Websockets.Messages.Generic;
using Hubcon.Shared.Core.Websockets.Models;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hubcon.Shared.Core.Websockets.Messages.Ingest
{
    public class IngestCompleteMessage : BaseMessage
    {
        private Guid[]? _streamIds;

        public IngestCompleteMessage(BaseMessage baseMessage) : base(baseMessage)
        {
            
        }

        public IngestCompleteMessage(TrimmedMemoryOwner buffer, Guid? id = null, string? connectionId = null, MessageType? type = null) : base(buffer, id, connectionId, type)
        {
        }

        [JsonConstructor]
        public IngestCompleteMessage(Guid id, string connectionId, Guid[] streamIds, string? error = null) : base(MessageType.ingest_complete, id, connectionId, error)
        {
            _streamIds = streamIds;
        }

        [JsonPropertyName("streamIds")]
        public Guid[]? StreamIds => _streamIds ??= Extract<Guid[]?>("streamIds")!;
    }

}
