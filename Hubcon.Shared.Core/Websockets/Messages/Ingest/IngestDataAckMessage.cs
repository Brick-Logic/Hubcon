using Hubcon.Shared.Core.Websockets.Messages.Generic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Hubcon.Shared.Core.Websockets.Messages.Ingest
{
    public record class IngestDataAckMessage : BaseMessage
    {
        public IngestDataAckMessage(ReadOnlyMemory<byte> buffer) : base(buffer)
        {
        }

        [JsonConstructor]
        public IngestDataAckMessage(Guid id) : base(MessageType.ingest_data_ack, id)
        {
        }
    }
}
