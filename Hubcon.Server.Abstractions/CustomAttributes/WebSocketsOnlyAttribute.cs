using Hubcon.Shared.Abstractions.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Server.Abstractions.CustomAttributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Method)]
    public sealed class WebSocketsAttribute : Attribute, ITransportAttribute
    {
        public readonly static ITransportAttribute Default = new WebSocketsAttribute();

        public string TransportKey => "WebSocket";
    }
}