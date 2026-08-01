using System;
using System.Collections.Generic;
using System.Text;

namespace Hubcon
{
    /// <summary>
    /// Use Hubcon's HTTP transport implementation. Should be used in the shared contract/interface for the client to adapt automatically. 
    /// <br/> <br/> Note that HTTP is unable to support Ingest operations due to transport limitations and it will throw an exception.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Method |
                    AttributeTargets.Property)]
    public class HttpTransport : HubconTransportAttribute
    {
        /// <inheritdoc/>
        public override string TransportKey => "Http";

        /// <inheritdoc/>
        public override int TelemetryId => 0;

        /// <inheritdoc/>
        public override TransportSettings DefaultTransportSettings { get; } = new TransportSettings()
        {
            MaxMessageSizeInBytes = 65535, // 64 KB
            RequestTimeout = TimeSpan.FromSeconds(15),
            ConnectionTimeout = TimeSpan.FromSeconds(15),
            MaxConnections = 10000,
            MaxConnectionsPerIp = 100,
            MaxConcurrentRequestsPerIp = 50,
            TransportPrefix = "/",

            EnablePing = false,
            EnablePong = false,
            PingOperationLimiterOptions = null,

            CallOperationEnabled = true,
            CallOperationTimeout = TimeSpan.FromSeconds(10),

            InvokeOperationEnabled = true,
            InvokeOperationTimeout = TimeSpan.FromSeconds(15),

            StreamOperationEnabled = true,
            StreamOperationTimeout = TimeSpan.FromMinutes(2),

            IngestOperationEnabled = false,
            IngestOperationTimeout = TimeSpan.Zero,

            UseRateLimiters = true,
            AllowAnonymousClients = true,
            RequiresAuth = false,
            AllowRemoteCancellation = true,
            RetryableMessagesEnabled = true,
            MethodOverloadingEnabled = false,
            LoggingEnabled = false,
            CheckTokenExpirationOnMessageReceived = false,

            TransportLimiterOptions = null,
            CallOperationLimiterOptions = null,
            InvokeOperationLimiterOptions = null,
            StreamOperationLimiterOptions = null,
            IngestOperationLimiterOptions = null,
            ControlMessagesRateLimiterOptions = null,

            TokenValidationParameters = null,
            ConnectionAuthHandlerType = null
        };
    }

    /// <summary>
    /// Use Hubcon's WebSocket transport implementation. Should be used in the shared contract/interface for the client to adapt automatically.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Method |
                    AttributeTargets.Property)]
    public sealed class WebSocketTransport : HubconTransportAttribute
    {
        /// <inheritdoc/>
        public override string TransportKey => "WebSocket";

        /// <inheritdoc/>
        public override int TelemetryId => 1;

        /// <inheritdoc/>
        public override TransportSettings DefaultTransportSettings { get; } = new TransportSettings()
        {
            MaxMessageSizeInBytes = 65535,
            RequestTimeout = TimeSpan.FromSeconds(30),
            ConnectionTimeout = TimeSpan.FromMinutes(60),
            MaxConnections = 50000,
            MaxConnectionsPerIp = 50,
            MaxConcurrentRequestsPerIp = 100,
            TransportPrefix = "/ws",

            EnablePing = true,
            EnablePong = true,

            CallOperationEnabled = true,
            CallOperationTimeout = TimeSpan.FromSeconds(10),

            InvokeOperationEnabled = true,
            InvokeOperationTimeout = TimeSpan.FromSeconds(30),

            StreamOperationEnabled = true,
            StreamOperationTimeout = TimeSpan.FromMinutes(30),

            IngestOperationEnabled = true,
            IngestOperationTimeout = TimeSpan.FromMinutes(30),

            UseRateLimiters = true,
            AllowAnonymousClients = true,
            RequiresAuth = false,
            AllowRemoteCancellation = true,
            RetryableMessagesEnabled = true,
            MethodOverloadingEnabled = false,
            LoggingEnabled = false,
            CheckTokenExpirationOnMessageReceived = true,

            PingOperationLimiterOptions = null,
            TransportLimiterOptions = null,
            CallOperationLimiterOptions = null,
            InvokeOperationLimiterOptions = null,
            StreamOperationLimiterOptions = null,
            IngestOperationLimiterOptions = null,
            ControlMessagesRateLimiterOptions = null,

            TokenValidationParameters = null,
            ConnectionAuthHandlerType = null
        };
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

        /// <inheritdoc/>
        public override TransportSettings DefaultTransportSettings { get; } = new TransportSettings()
        {
            MaxMessageSizeInBytes = 1048576,
            RequestTimeout = TimeSpan.FromSeconds(30),
            ConnectionTimeout = TimeSpan.FromSeconds(30),
            MaxConnections = 5000,
            MaxConnectionsPerIp = 20,
            MaxConcurrentRequestsPerIp = 10,
            TransportPrefix = "/",

            EnablePing = false,
            EnablePong = false,
            PingOperationLimiterOptions = null,

            CallOperationEnabled = true,
            CallOperationTimeout = TimeSpan.FromSeconds(15),

            InvokeOperationEnabled = true,
            InvokeOperationTimeout = TimeSpan.FromSeconds(30),

            StreamOperationEnabled = true,
            StreamOperationTimeout = TimeSpan.FromMinutes(2),

            IngestOperationEnabled = false,
            IngestOperationTimeout = TimeSpan.Zero,

            UseRateLimiters = true,
            AllowAnonymousClients = true,
            RequiresAuth = false,
            AllowRemoteCancellation = false,
            RetryableMessagesEnabled = false,
            MethodOverloadingEnabled = true,
            LoggingEnabled = true,
            CheckTokenExpirationOnMessageReceived = false,

            TransportLimiterOptions = null,
            CallOperationLimiterOptions = null,
            InvokeOperationLimiterOptions = null,
            StreamOperationLimiterOptions = null,
            IngestOperationLimiterOptions = null,
            ControlMessagesRateLimiterOptions = null,

            TokenValidationParameters = null,
            ConnectionAuthHandlerType = null
        };
    }
}