using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.IdentityModel.Tokens;

namespace Hubcon
{
    /// <summary>
    /// Use Hubcon's HTTP transport implementation. Should be used in the shared contract/interface for the client to adapt automatically. 
    /// <br/> <br/> Note that HTTP is unable to support Ingest operations due to transport limitations and it will throw an exception.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Method |
                    AttributeTargets.Property)]
    public class HttpTransport : HubconTransportAttribute<HttpTransportSettings>
    {
        /// <inheritdoc/>
        public override string TransportKey => "Http";

        /// <inheritdoc/>
        public override int TelemetryId => 0;
    }

    /// <inheritdoc/>
    public class HttpTransportSettings : TransportSettings
    {
        public HttpTransportSettings()
        {
            base.RequestTimeout = TimeSpan.FromSeconds(15);
            base.MaxConnections = 1000;
            base.MaxConnectionsPerIp = 10;
            base.EnablePing = true;
            base.EnablePong = true;
            base.TransportPrefix = "/";
            base.CallOperationEnabled = true;
            base.CallOperationTimeout = TimeSpan.FromSeconds(15);
            base.InvokeOperationEnabled = true;
            base.InvokeOperationTimeout = TimeSpan.FromSeconds(15);
            base.StreamOperationEnabled = true;
            base.StreamOperationTimeout = TimeSpan.FromSeconds(15);
            base.IngestOperationEnabled = false;
            base.IngestOperationTimeout = TimeSpan.MinValue;
            base.RetryableMessagesEnabled = false;
            base.UseRateLimiters = true;
            base.LoggingEnabled = true;
            base.AllowRemoteCancellation = true;
            base.MethodOverloadingEnabled = false;
            base.MaxConcurrentRequestsPerIp = 25;
            base.AllowAnonymousClients = true;
            base.CheckTokenExpirationOnMessageReceived = true;
            base.ConnectionTimeout = TimeSpan.FromSeconds(15);
            base.RequiresAuth = true;
        }
    }

    /// <summary>
    /// Use Hubcon's WebSocket transport implementation. Should be used in the shared contract/interface for the client to adapt automatically.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Method |
                    AttributeTargets.Property)]
    public sealed class WebSocketTransport : HubconTransportAttribute<WebSocketTransportSettings>
    {
        /// <inheritdoc/>
        public override string TransportKey => "WebSocket";

        /// <inheritdoc/>
        public override int TelemetryId => 1;
    }

    /// <inheritdoc/>
    public class WebSocketTransportSettings : TransportSettings
    {
        public WebSocketTransportSettings()
        {
            base.MaxMessageSizeInBytes = 65535;
            base.RequestTimeout = TimeSpan.FromSeconds(30);
            base.MaxConnections = 5000;
            base.MaxConnectionsPerIp = 20;
            base.EnablePing = true;
            base.EnablePong = true;
            base.TransportPrefix = "/ws";
            base.CallOperationEnabled = true;
            base.CallOperationTimeout = TimeSpan.FromSeconds(10);
            base.InvokeOperationEnabled = true;
            base.InvokeOperationTimeout  = TimeSpan.FromSeconds(10);
            base.StreamOperationEnabled  = true;
            base.StreamOperationTimeout = TimeSpan.FromSeconds(15);
            base.IngestOperationEnabled = true;
            base.IngestOperationTimeout = TimeSpan.FromSeconds(15);
            base.UseRateLimiters = true;
            base.LoggingEnabled = true;
            base.AllowRemoteCancellation = true;
            base.MethodOverloadingEnabled = true;
            base.MaxConcurrentRequestsPerIp = 25;
            base.AllowAnonymousClients = false;
            base.CheckTokenExpirationOnMessageReceived = true;
            base.ConnectionTimeout = TimeSpan.MaxValue;
            base.RequiresAuth = true;
        }

        /// <summary>
        /// Determines the heartbeat expiration seconds. If the connection does not receive a ping in time, it may be aborted.
        /// </summary>
        public int HeartBeatInSeconds { get; set; } = 120;
    }

    /// <summary>
    /// Use Non-Hubcon HTTP transport implementation. Should be used in the shared contract/interface for the client to adapt automatically.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Method |
                    AttributeTargets.Property)]
    public sealed class NonHubconHttpTransport : HubconTransportAttribute
    {
        /// <inheritdoc/>
        public override string TransportKey => "NonHubconHttp";

        /// <inheritdoc/>
        public override int TelemetryId => 2;
    }
}