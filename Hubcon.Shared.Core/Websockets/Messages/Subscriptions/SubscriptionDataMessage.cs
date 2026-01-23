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

        [JsonConstructor]
        public SubscriptionDataMessage(Guid id, JsonElement data) : base(MessageType.subscription_data, id)
        {
            _data = data;
        }

        public SubscriptionDataMessage(TrimmedMemoryOwner buffer, Guid? id = null, MessageType? type = null) : base(buffer, id, type)
        {
        }

        public JsonElement Data => _data ??= Extract<JsonElement>("data");
    }
}
