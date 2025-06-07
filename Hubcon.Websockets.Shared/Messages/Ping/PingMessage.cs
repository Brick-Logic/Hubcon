using Hubcon.Websockets.Shared.Messages.Generic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Websockets.Shared.Messages.Ping
{
    public record class PingMessage() : BaseMessage(MessageType.ping)
    {
        public Guid Id { get; } = Guid.NewGuid();
    }
}
