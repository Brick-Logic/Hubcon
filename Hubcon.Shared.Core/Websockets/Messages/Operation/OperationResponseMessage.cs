using Hubcon.Shared.Core.Websockets.Messages.Generic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Hubcon.Shared.Core.Websockets.Messages.Operation
{
    public record class OperationResponseMessage(string Id, JsonElement Result) : BaseMessage(MessageType.operation_response);
}
