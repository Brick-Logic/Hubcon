using Hubcon.Shared.Core.Websockets.Messages.Generic;
using Hubcon.Shared.Core.Websockets.Models;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;


namespace Hubcon.Shared.Core.Websockets.Messages.Operation
{
    public class OperationInvokeMessage : BaseMessage
    {
        private JsonElement? _payload;

        public OperationInvokeMessage(BaseMessage baseMessage) : base(baseMessage)
        {
            
        }

        public OperationInvokeMessage(TrimmedMemoryOwner buffer, Guid? id = null, MessageType? type = null) : base(buffer, id, type)
        {
        }

        [JsonConstructor]
        public OperationInvokeMessage(Guid id, JsonElement payload, string? error = null) : base(MessageType.operation_invoke, id, error)
        {
            _payload = payload;
        }

        [JsonPropertyName("payload")]
        public JsonElement Payload => _payload ??= Extract<JsonElement>("payload");
    }
}