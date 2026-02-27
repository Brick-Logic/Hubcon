using Hubcon.Shared.Core.Websockets.Messages.Generic;
using Hubcon.Shared.Core.Websockets.Models;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hubcon.Shared.Core.Websockets.Messages.Subscriptions
{
    public class SubscriptionCompleteMessage : BaseMessage
    {
        public SubscriptionCompleteMessage(BaseMessage baseMessage) : base(baseMessage)
        {
            
        }

        public SubscriptionCompleteMessage(TrimmedMemoryOwner buffer, Guid? id = null, MessageType? type = null) : base(buffer, id, type)
        {
        }

        [JsonConstructor]
        public SubscriptionCompleteMessage(Guid id, string? error = null) : base(MessageType.subscription_complete, id, error)
        {
        }
    }
}