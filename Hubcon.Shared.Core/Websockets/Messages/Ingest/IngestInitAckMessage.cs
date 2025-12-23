using Hubcon.Shared.Core.Websockets.Messages.Generic;
using System;
using System.Text.Json.Serialization;

namespace Hubcon.Shared.Core.Websockets.Messages.Ingest
{
    public class IngestInitAckMessage : BaseMessage
    {
        public IngestInitAckMessage(ReadOnlyMemory<byte> buffer, Guid? id = null, MessageType? type = null) : base(buffer, id, type)
        {
        }

        [JsonConstructor]
        public IngestInitAckMessage(Guid id) : base(MessageType.ingest_init_ack, id)
        {
        }
    }
}
