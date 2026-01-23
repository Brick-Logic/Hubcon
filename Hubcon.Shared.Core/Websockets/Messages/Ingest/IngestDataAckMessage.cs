using Hubcon.Shared.Core.Websockets.Messages.Generic;
using Hubcon.Shared.Core.Websockets.Models;
using System;
using System.Text.Json.Serialization;

namespace Hubcon.Shared.Core.Websockets.Messages.Ingest
{
    public class IngestDataAckMessage : BaseMessage
    {
        public IngestDataAckMessage(TrimmedMemoryOwner buffer, Guid? id = null, MessageType? type = null) : base(buffer, id, type)
        {
        }

        [JsonConstructor]
        public IngestDataAckMessage(Guid id) : base(MessageType.ingest_data_ack, id)
        {
        }
    }
}
