#pragma warning disable CS1591
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

        public PongMessage(TrimmedMemoryOwner buffer, Guid? id = null, string? connectionId = null, MessageType? type = null) : base(buffer, id, connectionId, type)
        {
        }

        [JsonConstructor]
        public PongMessage(Guid id, string connectionId, string? error = null) : base(MessageType.pong, id, connectionId, error)
        {
        }
    }
}
