#pragma warning disable CS1591
using Hubcon.Shared.Core.Websockets.Messages.Generic;
using Hubcon.Shared.Core.Websockets.Models;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hubcon.Shared.Core.Websockets.Messages.Token
{
    public class TokenUpdateResponseMessage : BaseMessage
    {
        private bool? _result;
        private string? _message;
        public TokenUpdateResponseMessage(BaseMessage baseMessage) : base(baseMessage)
        {
            
        }
        public TokenUpdateResponseMessage(TrimmedMemoryOwner buffer, Guid? id = null, MessageType? type = null) : base(buffer, id, type)
        {
        }

        [JsonConstructor]
        public TokenUpdateResponseMessage(Guid id, bool result, string message, string? error = null) : base(MessageType.token_update, id, error)
        {
            _result = result;
            _message = message;
        }

        public bool Result => _result ??= Extract<bool>("result")!;
        public string Message => _message ??= Extract<string>("message")!;
    }
}
