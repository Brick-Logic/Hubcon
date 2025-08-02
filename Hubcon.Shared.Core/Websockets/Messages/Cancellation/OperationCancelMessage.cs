using Hubcon.Shared.Core.Websockets.Messages.Generic;
using System.Text.Json.Serialization;

namespace Hubcon.Shared.Core.Websockets.Messages.Cancellation
{
    public sealed record class CancelMessage : BaseMessage
    {
        public CancelMessage(ReadOnlyMemory<byte> buffer) : base(buffer)
        {
        }

        [JsonConstructor]
        public CancelMessage(Guid id) : base(MessageType.cancel, id)
        {
        }
    }
}
