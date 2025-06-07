using Hubcon.Websockets.Shared.Messages.Generic;
using System.Text.Json;

namespace Hubcon.Websockets.Shared.Messages.Operation
{
    public record class OperationCallMessage(string Id, JsonElement Payload) : BaseMessage(MessageType.operation_call);

}
