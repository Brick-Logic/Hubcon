using Hubcon.Shared.Core.Websockets;
using Hubcon.Shared.Core.Websockets.Messages.Generic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Hubcon.Shared.Core.Websockets.Messages.Connection
{
    public record class ConnectionAckMessage : BaseMessage
    {
        public ConnectionAckMessage(ReadOnlyMemory<byte> buffer) : base(buffer)
        {
        }

        [JsonConstructor]
        public ConnectionAckMessage(Guid id) : base(MessageType.connection_ack, id)
        {
        }
    }
}
