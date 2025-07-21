using Hubcon.Shared.Core.Websockets;
using Hubcon.Shared.Core.Websockets.Messages.Generic;
using System.Text.Json;

namespace Hubcon.Shared.Core.Websockets.Messages.Operation
{
    public record class OperationCallMessage(Guid Id, JsonElement Payload) : BaseMessage(MessageType.operation_call, Id);

}
