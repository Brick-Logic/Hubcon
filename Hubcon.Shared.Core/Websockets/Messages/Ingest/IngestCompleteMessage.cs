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

        public IngestCompleteMessage(TrimmedMemoryOwner buffer, Guid? id = null, MessageType? type = null) : base(buffer, id, type)
        {
        }

        [JsonConstructor]
        public IngestCompleteMessage(Guid id, Guid[] streamIds, string? error = null) : base(MessageType.ingest_complete, id, error)
        {
            _streamIds = streamIds;
        }

        [JsonPropertyName("streamIds")]
        public Guid[]? StreamIds => _streamIds ??= Extract<Guid[]?>("streamIds")!;
    }

}
