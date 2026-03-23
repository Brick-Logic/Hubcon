#pragma warning disable CS1591
using Hubcon.Shared.Core.Websockets.Models;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hubcon.Shared.Core.Websockets.Messages.Generic
{
    public class AckMessage : BaseMessage
    {
        public AckMessage(BaseMessage baseMessage) : base(baseMessage)
        {
            
        }

        public AckMessage(TrimmedMemoryOwner buffer, Guid? id = null, MessageType? type = null) : base(buffer, id, type)
        {
        }

        [JsonConstructor]
        public AckMessage(Guid id, string? error = null) : base(MessageType.ack, id, error)
        {
        }
    }
}
