using Hubcon.Shared.Core.Websockets.Messages.Generic;
using System;
using System.Text.Json.Serialization;

namespace Hubcon.Shared.Core.Websockets.Messages.Token
{
    public class TokenUpdateMessage : BaseMessage
    {
        private string? _token;

        public TokenUpdateMessage(ReadOnlyMemory<byte> buffer, Guid? id = null, MessageType? type = null) : base(buffer, id, type)
        {
        }

        [JsonConstructor]
        public TokenUpdateMessage(Guid id, string token) : base(MessageType.token_update, id)
        {
            _token = token;
        }

        public string Token => _token ??= Extract<string>("token")!;
    }
}
