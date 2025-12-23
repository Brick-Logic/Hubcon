using System;
using System.Text.Json.Serialization;

namespace Hubcon.Shared.Core.Websockets.Messages.Generic
{
    public class ErrorMessage : BaseMessage
    {
        private string? _error;
        private object? _payload;

        public ErrorMessage(ReadOnlyMemory<byte> buffer, Guid? id = null, MessageType? type = null) : base(buffer, id, type)
        {
        }

        [JsonConstructor]
        public ErrorMessage(Guid id, string? Error = null, object? Payload = null) : base(MessageType.error, id)
        {
            _error = Error;
            _payload = Payload;
        }

        public string? Error => _error ??= Extract<string>("error");
        public object? Payload => _payload ??= Extract<string>("payload");
    }
}
