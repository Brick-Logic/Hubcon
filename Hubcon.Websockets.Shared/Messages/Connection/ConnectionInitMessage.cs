using Hubcon.Websockets.Shared.Messages.Generic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Websockets.Shared.Messages.Connection
{
    public record class ConnectionInitMessage() : BaseMessage(MessageType.connection_init);
}
