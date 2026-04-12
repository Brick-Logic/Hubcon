#pragma warning disable CS1591
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
        public ErrorMessage(TrimmedMemoryOwner buffer, Guid? id = null, string? connectionId = null, MessageType? type = null) : base(buffer, id, connectionId, type)
        {
        }

        [JsonConstructor]
        public ErrorMessage(Guid id, string connectionId, string error) : base(MessageType.error, id, connectionId, error)
        {
        }
    }
}
