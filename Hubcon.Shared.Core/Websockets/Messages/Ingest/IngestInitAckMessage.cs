#pragma warning disable CS1591
using Hubcon.Shared.Core.Websockets.Messages.Generic;
using Hubcon.Shared.Core.Websockets.Models;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hubcon.Shared.Core.Websockets.Messages.Ingest
{
    public class IngestInitAckMessage : BaseMessage
    {
        public IngestInitAckMessage(BaseMessage baseMessage) : base(baseMessage)
        {
            
        }
        public IngestInitAckMessage(TrimmedMemoryOwner buffer, Guid? id = null, MessageType? type = null) : base(buffer, id, type)
        {
        }

        [JsonConstructor]
        public IngestInitAckMessage(Guid id, string? error = null) : base(MessageType.ingest_init_ack, id, error)
        {
        }
    }
}
