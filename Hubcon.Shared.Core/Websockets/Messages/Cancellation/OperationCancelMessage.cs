#pragma warning disable CS1591
using Hubcon.Shared.Core.Websockets.Messages.Generic;
using Hubcon.Shared.Core.Websockets.Models;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hubcon.Shared.Core.Websockets.Messages.Cancellation
{
    public sealed class CancelMessage : BaseMessage
    {
        public CancelMessage(BaseMessage baseMessage) : base(baseMessage)
        {
        }

        public CancelMessage(TrimmedMemoryOwner buffer, Guid? id = null, string? connectionId = null, MessageType? type = null) : base(buffer, id, connectionId, type)
        {
        }

        [JsonConstructor]
        public CancelMessage(Guid id, string connectionId, string? error = null) : base(MessageType.cancel, id, connectionId, error)
        {
        }
    }
}
