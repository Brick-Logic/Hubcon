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
        public IngestInitAckMessage(TrimmedMemoryOwner buffer, Guid? id = null, string? connectionId = null, MessageType? type = null) : base(buffer, id, connectionId, type)
        {
        }

        [JsonConstructor]
        public IngestInitAckMessage(Guid id, string connectionId, string? error = null) : base(MessageType.ingest_init_ack, id, connectionId, error)
        {
        }
    }
}
