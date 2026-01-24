using Hubcon.Shared.Core.Websockets.Models;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hubcon.Shared.Core.Websockets.Messages.Generic
{
    public class ErrorMessage : BaseMessage
    {
        private string? _error;
        private JsonElement? _payload;

        public ErrorMessage(TrimmedMemoryOwner buffer, Guid? id = null, MessageType? type = null) : base(buffer, id, type)
        {
        }

        [JsonConstructor]
        public ErrorMessage(Guid id, JsonElement? Payload = null) : base(MessageType.error, id)
        {
            _error = Error;
            _payload = Payload;
        }

        [JsonPropertyName("error")]
        public string? Error => _error ??= Extract<string>("error");

        [JsonPropertyName("payload")]
        public JsonElement? Payload => _payload ??= Extract<JsonElement>("payload");
    }
}
