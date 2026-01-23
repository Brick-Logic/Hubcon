using Hubcon.Shared.Core.Websockets.Messages.Generic;
using Hubcon.Shared.Core.Websockets.Models;
using System;
using System.Text.Json.Serialization;

namespace Hubcon.Shared.Core.Websockets.Messages.Connection
{
    public class ConnectionAckMessage : BaseMessage
    {
        public ConnectionAckMessage(TrimmedMemoryOwner buffer, Guid? id = null, MessageType? type = null) : base(buffer, id, type)
        {
        }

        [JsonConstructor]
        public ConnectionAckMessage(Guid id) : base(MessageType.connection_ack, id)
        {
        }
    }
}
