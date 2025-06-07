using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Shared.Core.Websockets.Messages.Generic
{
    public record class AckMessage() : BaseMessage(MessageType.ack)
    {
        public Guid Id { get; set; }
    }
}
