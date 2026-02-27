using Hubcon.Shared.Core.Websockets.Messages.Generic;
using Hubcon.Shared.Core.Websockets.Models;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;


namespace Hubcon.Shared.Core.Websockets.Messages.Operation
{
    public class OperationCallMessage : BaseMessage
    {
        private JsonElement? _payload;

        public OperationCallMessage(BaseMessage baseMessage) : base(baseMessage)
        {
            
        }

        public OperationCallMessage(TrimmedMemoryOwner buffer, Guid? id = null, MessageType? type = null) : base(buffer, id, type)
        {
        }

        [JsonConstructor]
        public OperationCallMessage(Guid id, JsonElement payload, string? error = null) : base(MessageType.operation_call, id, error)
        {
            _payload = payload;
        }

        public JsonElement Payload => _payload ??= Extract<JsonElement>("payload");
    }
}
