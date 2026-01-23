using Hubcon.Shared.Core.Websockets.Messages.Generic;
using Hubcon.Shared.Core.Websockets.Models;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hubcon.Shared.Core.Websockets.Messages.Subscriptions
{
    public class SubscriptionDataWithAckMessage : BaseMessage
    {
        private JsonElement? _data;
        private Guid? _ackId;

        public SubscriptionDataWithAckMessage(TrimmedMemoryOwner buffer, Guid? id = null, MessageType? type = null) : base(buffer, id, type)
        {
        }

        [JsonConstructor]
        public SubscriptionDataWithAckMessage(Guid id, JsonElement data, Guid ackId) : base(MessageType.subscription_data_with_ack, id)
        {
            _data = data;
            _ackId = ackId;
        }

        public JsonElement Data => _data ??= Extract<JsonElement>("data");
        public Guid AckId => _ackId ??= Extract<Guid>("ackId");
    }
}
