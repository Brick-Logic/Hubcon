using Hubcon.Websockets.Shared.Messages.Generic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Hubcon.Websockets.Shared.Messages.Subscriptions
{
    public record class EventDataMessage(string Id, JsonElement Data) : BaseMessage(MessageType.subscription_event_data);
}
