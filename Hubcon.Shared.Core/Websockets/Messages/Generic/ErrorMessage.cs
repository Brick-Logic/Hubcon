using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Hubcon.Shared.Core.Websockets.Messages.Generic
{
    public record class ErrorMessage : BaseMessage
    {
        private string? _error;
        private object? _payload;

        public ErrorMessage(ReadOnlyMemory<byte> buffer) : base(buffer)
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
