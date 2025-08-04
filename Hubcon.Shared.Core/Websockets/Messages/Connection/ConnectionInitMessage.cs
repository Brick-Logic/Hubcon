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
    public record class ConnectionInitMessage : BaseMessage
    {
        public ConnectionInitMessage(ReadOnlyMemory<byte> buffer, Guid? id = null, MessageType? type = null) : base(buffer, id, type)
        {
        }

        [JsonConstructor]
        public ConnectionInitMessage(Guid id) : base(MessageType.connection_init, id)
        {
        }
    }
}
