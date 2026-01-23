using Hubcon.Shared.Core.Websockets.Messages.Generic;
using Hubcon.Shared.Core.Websockets.Models;
using System;
using System.Text.Json.Serialization;

namespace Hubcon.Shared.Core.Websockets.Messages.Ping
{
    public class PongMessage : BaseMessage
    {
        public PongMessage(TrimmedMemoryOwner buffer, Guid? id = null, MessageType? type = null) : base(buffer, id, type)
        {
        }

        [JsonConstructor]
        public PongMessage(Guid id) : base(MessageType.pong, id)
        {
        }
    }
}
