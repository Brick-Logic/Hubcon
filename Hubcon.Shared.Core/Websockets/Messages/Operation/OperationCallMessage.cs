using Hubcon.Shared.Core.Websockets;
using Hubcon.Shared.Core.Websockets.Messages.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hubcon.Shared.Core.Websockets.Messages.Operation
{
    public record class OperationCallMessage : BaseMessage
    {
        private JsonElement? _payload;

        public OperationCallMessage(ReadOnlyMemory<byte> buffer, Guid? id = null, MessageType? type = null) : base(buffer, id, type)
        {
        }

        [JsonConstructor]
        public OperationCallMessage(Guid id, JsonElement payload) : base(MessageType.operation_call, id)
        {
            _payload = payload;
        }

        public JsonElement Payload => _payload ??= Extract<JsonElement>("payload");
    }
}
