using System;
using System.Collections.Generic;
using System.Text;

namespace Hubcon
{
    /// <summary>
    /// Use Hubcon's HTTP transport implementation. Should be used in the shared contract/interface for the client to adapt automatically. 
    /// <br/> <br/> Note that HTTP is unable to support Ingest operations due to transport limitations and it will throw an exception.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Method | AttributeTargets.Property)]
    public class HttpTransport : HubconTransportAttribute
    {
        /// <inheritdoc/>
        public override string TransportKey => "Http";
        
        /// <inheritdoc/>
        public override int TelemetryId => 0;
    }

    /// <summary>
    /// Use Hubcon's WebSocket transport implementation. Should be used in the shared contract/interface for the client to adapt automatically.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Method | AttributeTargets.Property)]
    public sealed class WebSocketTransport : HubconTransportAttribute
    {
        /// <inheritdoc/>
        public override string TransportKey => "WebSocket";
        
        /// <inheritdoc/>
        public override int TelemetryId => 1;
    }

    /// <summary>
    /// Use Non-Hubcon HTTP transport implementation. Should be used in the shared contract/interface for the client to adapt automatically.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Method | AttributeTargets.Property)]
    public sealed class NonHubconHttpTransport : HubconTransportAttribute
    {
        /// <inheritdoc/>
        public override string TransportKey => "NonHubconHttp";
        
        /// <inheritdoc/>
        public override int TelemetryId => 2;
    }
}