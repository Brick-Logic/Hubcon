using Hubcon.Shared.Core.Websockets;
using Hubcon.Shared.Core.Websockets.Messages.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hubcon.Shared.Core.Websockets.Messages.Operation
{
    public record class OperationInvokeMessage : BaseMessage
    {
        private JsonElement? _payload;

        public OperationInvokeMessage(ReadOnlyMemory<byte> buffer) : base(buffer)
        {
        }

        [JsonConstructor]
        public OperationInvokeMessage(Guid id, JsonElement payload) : base(MessageType.operation_invoke, id)
        {
            _payload = payload;
        }

        public JsonElement Payload => _payload ??= Extract<JsonElement>("payload");
    }
}