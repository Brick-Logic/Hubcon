using Hubcon.Shared.Core.Websockets;
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
    public record class SubscriptionDataWithAckMessage : BaseMessage
    {
        private JsonElement? _data;
        private Guid? _ackId;

        public SubscriptionDataWithAckMessage(ReadOnlyMemory<byte> buffer) : base(buffer)
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
