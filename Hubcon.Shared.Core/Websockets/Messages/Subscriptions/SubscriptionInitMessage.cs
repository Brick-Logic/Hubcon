using Hubcon.Shared.Core.Websockets;
using Hubcon.Shared.Core.Websockets.Messages.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hubcon.Shared.Core.Websockets.Messages.Subscriptions
{
    public record class SubscriptionInitMessage : BaseMessage
    {
        private JsonElement? _payload;

        public SubscriptionInitMessage(ReadOnlyMemory<byte> buffer) : base(buffer)
        {
        }

        [JsonConstructor]
        public SubscriptionInitMessage(Guid id, JsonElement payload) : base(MessageType.subscription_init, id)
        {
            _payload = payload;
        }

        public JsonElement Payload => _payload ??= Extract<JsonElement>("payload");
    }
}
