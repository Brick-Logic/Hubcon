using Hubcon.Websockets.Shared;
using Hubcon.Websockets.Shared.Messages.Generic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Websockets.Shared.Messages.Subscriptions
{
    public record class UnsubscribeMessage(string SubscriptionId) : BaseMessage(MessageType.subscription_cancel);
}