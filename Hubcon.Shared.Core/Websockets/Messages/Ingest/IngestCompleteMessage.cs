using Hubcon.Shared.Core.Websockets.Messages.Generic;
using System;
using System.Text.Json.Serialization;

namespace Hubcon.Shared.Core.Websockets.Messages.Ingest
{
    public class IngestCompleteMessage : BaseMessage
    {
        private Guid[]? _streamIds;

        public IngestCompleteMessage(ReadOnlyMemory<byte> buffer, Guid? id = null, MessageType? type = null) : base(buffer, id, type)
        {
        }

        [JsonConstructor]
        public IngestCompleteMessage(Guid id, Guid[] streamIds) : base(MessageType.ingest_complete, id)
        {
            _streamIds = streamIds;
        }

        [JsonPropertyName("streamIds")]
        public Guid[] StreamIds => _streamIds ??= Extract<Guid[]>("streamIds");
    }

}
