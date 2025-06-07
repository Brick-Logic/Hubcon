using Hubcon.Websockets.Shared;
using Hubcon.Websockets.Shared.Messages.Generic;
using System.Text.Json;

namespace Hubcon.Websockets.Shared.Messages.Subscriptions
{
    public record class SubscribeMessage(string SubscriptionId, JsonElement Payload) : BaseMessage(MessageType.subscription_init);
}
