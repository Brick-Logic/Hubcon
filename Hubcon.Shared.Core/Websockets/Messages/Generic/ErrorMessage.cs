using Hubcon.Shared.Core.Websockets.Models;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hubcon.Shared.Core.Websockets.Messages.Generic
{
    public class ErrorMessage : BaseMessage
    {
        public ErrorMessage(BaseMessage baseMessage) : base(baseMessage)
        {
            
        }
        public ErrorMessage(TrimmedMemoryOwner buffer, Guid? id = null, MessageType? type = null) : base(buffer, id, type)
        {
        }

        [JsonConstructor]
        public ErrorMessage(Guid id, string error) : base(MessageType.error, id, error)
        {
        }
    }
}
