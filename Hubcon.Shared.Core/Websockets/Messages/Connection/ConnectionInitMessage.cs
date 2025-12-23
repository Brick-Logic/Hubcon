using Hubcon.Shared.Core.Websockets.Messages.Generic;
using System;
using System.Text.Json.Serialization;

namespace Hubcon.Shared.Core.Websockets.Messages.Connection
{
    public class ConnectionInitMessage : BaseMessage
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
