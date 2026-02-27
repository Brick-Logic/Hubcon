using Hubcon.Shared.Core.Websockets.Messages.Generic;
using Hubcon.Shared.Core.Websockets.Models;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hubcon.Shared.Core.Websockets.Messages.Ping
{
    public class PongMessage : BaseMessage
    {
        public PongMessage(BaseMessage baseMessage) : base(baseMessage)
        {
            
        }

        public PongMessage(TrimmedMemoryOwner buffer, Guid? id = null, MessageType? type = null) : base(buffer, id, type)
        {
        }

        [JsonConstructor]
        public PongMessage(Guid id, string? error = null) : base(MessageType.pong, id, error)
        {
        }
    }
}
