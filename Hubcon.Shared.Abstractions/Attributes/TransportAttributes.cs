using System;
using System.Collections.Generic;
using System.Text;

namespace Hubcon
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Method | AttributeTargets.Property)]
    public class HttpTransport : HubconTransportAttribute
    {
        public override string TransportKey => "Http";
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Method | AttributeTargets.Property)]
    public sealed class WebSocketTransport : HubconTransportAttribute
    {
        public override string TransportKey => "WebSocket";
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Method | AttributeTargets.Property)]
    public sealed class NonHubconHttpTransport : HubconTransportAttribute
    {
        public override string TransportKey => "NonHubconHttp";
    }
}