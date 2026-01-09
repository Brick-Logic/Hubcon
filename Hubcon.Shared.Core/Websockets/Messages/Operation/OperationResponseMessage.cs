using Hubcon.Shared.Core.Websockets.Messages.Generic;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hubcon.Shared.Core.Websockets.Messages.Operation
{
    public class OperationResponseMessage : BaseMessage
    {
        private JsonElement? _result;

        public OperationResponseMessage(ReadOnlyMemory<byte> buffer, Guid? id = null, MessageType? type = null) : base(buffer, id, type)
        {
        }

        [JsonConstructor]
        public OperationResponseMessage(Guid id, JsonElement result) : base(MessageType.operation_response, id)
        {
            _result = result;
        }

        [JsonPropertyName("result")]
        public JsonElement Result => _result ??= Extract<JsonElement>("result");
    }
}
