#pragma warning disable CS1591
using Hubcon.Shared.Core.Websockets.Messages.Generic;
using Hubcon.Shared.Core.Websockets.Models;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hubcon.Shared.Core.Websockets.Messages.Ingest
{
    public class IngestDataAckMessage : BaseMessage
    {
        public IngestDataAckMessage(BaseMessage baseMessage) : base(baseMessage)
        {
            
        }

        public IngestDataAckMessage(TrimmedMemoryOwner buffer, Guid? id = null, string? connectionId = null, MessageType? type = null) : base(buffer, id, connectionId, type)
        {
        }

        [JsonConstructor]
        public IngestDataAckMessage(Guid id, string connectionId, string? error = null) : base(MessageType.ingest_data_ack, id, connectionId, error)
        {
        }
    }
}
