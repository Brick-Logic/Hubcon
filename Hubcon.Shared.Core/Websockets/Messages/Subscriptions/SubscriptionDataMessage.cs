using Hubcon.Shared.Core.Websockets.Messages.Generic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Hubcon.Shared.Core.Websockets.Messages.Subscriptions
{
    public record class SubscriptionDataMessage : BaseMessage
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

        public SubscriptionDataMessage(ReadOnlyMemory<byte> buffer, Guid? id = null, MessageType? type = null) : base(buffer, id, type)
        {
        }

        public JsonElement Data => _data ??= Extract<JsonElement>("data");
    }
}
