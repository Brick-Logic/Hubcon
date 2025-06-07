using Hubcon.Websockets.Shared.Messages.Generic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Hubcon.Websockets.Shared.Messages.Operation
{
    public record class OperationResponseMessage(string Id, JsonElement Result) : BaseMessage(MessageType.operation_response);
}
