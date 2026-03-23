#pragma warning disable CS1591
using Hubcon.Shared.Core.Websockets.Messages.Generic;
using Hubcon.Shared.Core.Websockets.Models;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hubcon.Shared.Core.Websockets.Messages.Token
{
    public class TokenUpdateMessage : BaseMessage
    {
        private string? _token;

        public TokenUpdateMessage(BaseMessage baseMessage) : base(baseMessage)
        {
            
        }

        public TokenUpdateMessage(TrimmedMemoryOwner buffer, Guid? id = null, MessageType? type = null) : base(buffer, id, type)
        {
        }

        [JsonConstructor]
        public TokenUpdateMessage(Guid id, string token, string? error = null) : base(MessageType.token_update, id, error)
        {
            _token = token;
        }

        public string Token => _token ??= Extract<string>("token")!;
    }
}
