using Hubcon.Shared.Core.Websockets.Messages.Generic;
using Hubcon.Shared.Core.Websockets.Models;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hubcon.Shared.Core.Websockets.Messages.Ping
{
    public class PingMessage : BaseMessage
    {
        public PingMessage(BaseMessage baseMessage) : base(baseMessage)
        {
            
        }

        public PingMessage(TrimmedMemoryOwner buffer, Guid? id = null, MessageType? type = null) : base(buffer, id, type)
        {
        }

        [JsonConstructor]
        public PingMessage(Guid id, string? error = null) : base(MessageType.ping, id, error)
        {
        }
    }
}
