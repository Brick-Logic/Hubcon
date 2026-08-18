#pragma warning disable CS1591
using Hubcon.Shared.Core.Websockets.Messages.Generic;
using Hubcon.Shared.Core.Websockets.Models;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hubcon.Shared.Core.Websockets.Messages.Connection
{
    public class ConnectionInitMessage : BaseMessage
    {
        private string? _token;

        public ConnectionInitMessage(BaseMessage baseMessage) : base(baseMessage)
        {
            
        }
        public ConnectionInitMessage(TrimmedMemoryOwner buffer, Guid? id = null, string? connectionId = null, MessageType? type = null) : base(buffer, id, connectionId, type)
        {
        }

        [JsonConstructor]
        public ConnectionInitMessage(Guid id, string? error = null, string? token = null) : base(MessageType.connection_init, id, null!, error)
        {
            _token = token;
        }
        
        public string? Token => _token ??= Extract<string>("token")!;
    }
}