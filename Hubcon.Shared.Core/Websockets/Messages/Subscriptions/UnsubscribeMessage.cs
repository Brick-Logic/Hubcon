using Hubcon.Shared.Core.Websockets.Messages.Generic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Shared.Core.Websockets.Messages.Subscriptions
{
    public record class UnsubscribeMessage(string SubscriptionId) : BaseMessage(MessageType.subscription_cancel);
}