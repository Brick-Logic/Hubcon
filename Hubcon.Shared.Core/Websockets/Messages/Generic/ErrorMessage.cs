using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Hubcon.Shared.Core.Websockets.Messages.Generic
{
    public record class ErrorMessage(string Id, string? Error = null, object? Payload = null) : BaseMessage(MessageType.error, Id);
}
