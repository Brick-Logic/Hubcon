using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Hubcon.Shared.Core.Websockets.Messages.Generic
{
    public record class AckMessage : BaseMessage
    {
        public AckMessage(ReadOnlyMemory<byte> buffer) : base(buffer)
        {
        }

        [JsonConstructor]
        public AckMessage(Guid id) : base(MessageType.ack, id)
        {
        }
    }
}
