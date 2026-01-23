using Hubcon.Shared.Core.Websockets.Messages.Generic;
using Hubcon.Shared.Core.Websockets.Models;
using System;
using System.Text.Json.Serialization;

namespace Hubcon.Shared.Core.Websockets.Messages.Cancellation
{
    public sealed class CancelMessage : BaseMessage
    {
        public CancelMessage(TrimmedMemoryOwner buffer, Guid? id = null, MessageType? type = null) : base(buffer, id, type)
        {
        }

        [JsonConstructor]
        public CancelMessage(Guid id) : base(MessageType.cancel, id)
        {
        }
    }
}
