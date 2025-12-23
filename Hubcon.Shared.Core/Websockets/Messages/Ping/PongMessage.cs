using Hubcon.Shared.Core.Websockets.Messages.Generic;
using System;
using System.Text.Json.Serialization;

namespace Hubcon.Shared.Core.Websockets.Messages.Ping
{
    public class PongMessage : BaseMessage
    {
        public PongMessage(ReadOnlyMemory<byte> buffer, Guid? id = null, MessageType? type = null) : base(buffer, id, type)
        {
        }

        [JsonConstructor]
        public PongMessage(Guid id) : base(MessageType.pong, id)
        {
        }
    }
}
