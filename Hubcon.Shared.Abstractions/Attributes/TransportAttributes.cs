using System;
using System.Collections.Generic;
using System.Text;

namespace Hubcon
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Method)]
    public class HttpTransport : HubconTransportAttribute
    {
        public override string TransportKey => "Http";
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Method)]
    public sealed class WebSocketTransport : HubconTransportAttribute
    {
        public override string TransportKey => "WebSocket";
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Method)]
    public sealed class NonHubconHttpTransport : HubconTransportAttribute
    {
        public override string TransportKey => "NonHubconHttp";
    }
}