using Hubcon.Shared.Core.Websockets.Messages.Generic;
using Hubcon.Shared.Core.Websockets.Models;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hubcon.Shared.Core.Websockets.Messages.Subscriptions
{
    public class SubscriptionDataMessage : BaseMessage
    {
        private JsonElement? _data;

        public SubscriptionDataMessage()
        {
        }

        public SubscriptionDataMessage(BaseMessage baseMessage) : base(baseMessage)
        {
            
        }

        [JsonConstructor]
        public SubscriptionDataMessage(Guid id, JsonElement data, string? error = null) : base(MessageType.subscription_data, id, error)
        {
            _data = data;
        }

        public SubscriptionDataMessage(TrimmedMemoryOwner buffer, Guid? id = null, MessageType? type = null) : base(buffer, id, type)
        {
        }

        public JsonElement Data => _data ??= Extract<JsonElement>("data");
    }
}
