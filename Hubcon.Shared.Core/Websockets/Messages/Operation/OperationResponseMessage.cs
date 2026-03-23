#pragma warning disable CS1591
using Hubcon.Shared.Core.Websockets.Messages.Generic;
using Hubcon.Shared.Core.Websockets.Models;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hubcon.Shared.Core.Websockets.Messages.Operation
{
    public class OperationResponseMessage : BaseMessage
    {
        private JsonElement? _result;

        public OperationResponseMessage(BaseMessage baseMessage) : base(baseMessage)
        {
        }

        public OperationResponseMessage(TrimmedMemoryOwner buffer, Guid? id = null, MessageType? type = null) : base(buffer, id, type)
        {
        }

        [JsonConstructor]
        public OperationResponseMessage(Guid id, JsonElement result, string? error = null) : base(MessageType.operation_response, id, error)
        {
            _result = result;
        }

        [JsonPropertyName("result")]
        public JsonElement Result => _result ??= Extract<JsonElement>("result");
    }
}
