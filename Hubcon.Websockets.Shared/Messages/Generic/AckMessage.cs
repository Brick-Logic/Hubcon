using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Websockets.Shared.Messages.Generic
{
    public record class AckMessage() : BaseMessage(MessageType.ack)
    {
        public Guid Id { get; set; }
    }
}
