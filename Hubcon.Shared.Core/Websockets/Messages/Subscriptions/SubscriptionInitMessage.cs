using Hubcon.Shared.Core.Websockets.Messages.Generic;
using Hubcon.Shared.Core.Websockets.Models;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;


namespace Hubcon.Shared.Core.Websockets.Messages.Subscriptions
{
    public class SubscriptionInitMessage : BaseMessage
    {
        private JsonElement? _payload;

        public SubscriptionInitMessage(BaseMessage baseMessage) : base(baseMessage)
        {
            
        }
        public SubscriptionInitMessage(TrimmedMemoryOwner buffer, Guid? id = null, MessageType? type = null) : base(buffer, id, type)
        {
        }

        [JsonConstructor]
        public SubscriptionInitMessage(Guid id, JsonElement payload, string? error = null) : base(MessageType.subscription_init, id, error)
        {
            _payload = payload;
        }

        [JsonPropertyName("payload")]
        public JsonElement Payload => _payload ??= Extract<JsonElement>("payload");
    }
}
