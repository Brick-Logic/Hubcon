using Hubcon.Shared.Core.Websockets.Messages.Generic;
using Hubcon.Shared.Core.Websockets.Models;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hubcon.Shared.Core.Websockets.Messages.Connection
{
    public class ConnectionInitMessage : BaseMessage
    {
        public ConnectionInitMessage(BaseMessage baseMessage) : base(baseMessage)
        {
            
        }
        public ConnectionInitMessage(TrimmedMemoryOwner buffer, Guid? id = null, MessageType? type = null) : base(buffer, id, type)
        {
        }

        [JsonConstructor]
        public ConnectionInitMessage(Guid id, string? error = null) : base(MessageType.connection_init, id, error)
        {
        }
    }
}
