using Hubcon.Shared.Core.Websockets.Messages.Generic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Hubcon.Shared.Core.Websockets.Messages.Subscriptions
{
    public record class SubscriptionCompleteMessage : BaseMessage
    {
        public SubscriptionCompleteMessage(ReadOnlyMemory<byte> buffer, Guid? id = null, MessageType? type = null) : base(buffer, id, type)
        {
        }

        [JsonConstructor]
        public SubscriptionCompleteMessage(Guid id) : base(MessageType.subscription_complete, id)
        {
        }
    }
}