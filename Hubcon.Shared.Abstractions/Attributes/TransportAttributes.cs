using System;
using System.Collections.Generic;
using System.Text;

namespace Hubcon
{
    /// <summary>
    /// Use Hubcon's HTTP transport implementation. Should be used in the shared contract/interface.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Method | AttributeTargets.Property)]
    public class HttpTransport : HubconTransportAttribute
    {
        public override string TransportKey => "Http";
    }

    /// <summary>
    /// Use Hubcon's WebSocket transport implementation. Should be used in the shared contract/interface.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Method | AttributeTargets.Property)]
    public sealed class WebSocketTransport : HubconTransportAttribute
    {
        public override string TransportKey => "WebSocket";
    }

    /// <summary>
    /// Use Non-Hubcon HTTP transport implementation. Should be used in the shared contract/interface.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Method | AttributeTargets.Property)]
    public sealed class NonHubconHttpTransport : HubconTransportAttribute
    {
        public override string TransportKey => "NonHubconHttp";
    }
}