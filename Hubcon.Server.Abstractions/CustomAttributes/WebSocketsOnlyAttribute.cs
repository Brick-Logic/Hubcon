using Hubcon.Shared.Abstractions.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon
{
    public sealed class WebSocketTransport : HubconTransport
    {
        public override string TransportKey { get; } = "WebSocket";
    }
}