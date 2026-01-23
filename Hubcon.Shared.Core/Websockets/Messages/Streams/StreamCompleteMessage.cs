using Hubcon.Shared.Core.Websockets.Messages.Generic;
using Hubcon.Shared.Core.Websockets.Models;
using System;
using System.Text.Json.Serialization;

namespace Hubcon.Shared.Core.Websockets.Messages.Streams
{
    public class StreamCompleteMessage : BaseMessage
    {
        public StreamCompleteMessage(TrimmedMemoryOwner buffer, Guid? id = null, MessageType? type = null) : base(buffer, id, type)
        {
        }

        [JsonConstructor]
        public StreamCompleteMessage(Guid id) : base(MessageType.stream_complete, id)
        {
        }
    }
}