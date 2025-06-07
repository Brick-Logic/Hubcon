using Hubcon.Websockets.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Hubcon.Websockets.Shared.Messages.Generic
{
    public record class ErrorMessage() : BaseMessage(MessageType.error) 
    {
        public string? SubscriptionId { get; set; }
        public string? Error { get; set; }
        public JsonElement Payload { get; set; }
    }
}
