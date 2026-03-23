#pragma warning disable CS1591
using Hubcon.Shared.Core.Websockets.Messages.Generic;
using Hubcon.Shared.Core.Websockets.Models;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hubcon.Shared.Core.Websockets.Messages.Streams
{
    public class StreamCompleteMessage : BaseMessage
    {
        public StreamCompleteMessage(BaseMessage baseMessage) : base(baseMessage)
        {
            
        }

        public StreamCompleteMessage(TrimmedMemoryOwner buffer, Guid? id = null, MessageType? type = null) : base(buffer, id, type)
        {
        }

        [JsonConstructor]
        public StreamCompleteMessage(Guid id, string? error = null) : base(MessageType.stream_complete, id, error)
        {
        }
    }
}