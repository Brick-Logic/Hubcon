using System;
using System.Text.Json.Serialization;

namespace Hubcon.Shared.Core.Websockets.Messages.Generic
{
    public class AckMessage : BaseMessage
    {
        public AckMessage(ReadOnlyMemory<byte> buffer, Guid? id = null, MessageType? type = null) : base(buffer, id, type)
        {
        }

        [JsonConstructor]
        public AckMessage(Guid id) : base(MessageType.ack, id)
        {
        }
    }
}
