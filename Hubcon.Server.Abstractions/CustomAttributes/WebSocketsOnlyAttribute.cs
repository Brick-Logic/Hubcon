using Hubcon.Shared.Abstractions.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Server.Abstractions.CustomAttributes
{
    public sealed class WebSocketsAttribute : TransportAttribute
    {
        public readonly static TransportAttribute Default = new WebSocketsAttribute();

        public override string TransportKey => "WebSocket";
    }
}