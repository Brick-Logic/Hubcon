using Hubcon.Shared.Core.Websockets;
using Hubcon.Shared.Core.Websockets.Messages.Generic;
using System.Text.Json;

namespace Hubcon.Shared.Core.Websockets.Messages.Subscriptions
{
    public record class SubscriptionInitMessage(Guid Id, JsonElement Payload) : BaseMessage(MessageType.subscription_init, Id);
}
