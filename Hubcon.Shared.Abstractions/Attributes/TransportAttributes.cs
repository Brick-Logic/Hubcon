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
        /// <inheritdoc cref="ITransportSettings.RequestTimeout" />
        public override TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(15);

        /// <inheritdoc cref="ITransportSettings.MaxConnections" />
        public override int MaxConnections { get; set; } = 1000;

        /// <inheritdoc cref="ITransportSettings.MaxConnectionsPerIp" />
        public override int MaxConnectionsPerIp { get; set; } = 10;

        /// <inheritdoc cref="ITransportSettings.EnablePing" />
        public override bool EnablePing { get; set; } = true;

        /// <inheritdoc cref="ITransportSettings.PingOperationLimiterOptions" />
        public override TokenBucketRateLimiterOptions? PingOperationLimiterOptions { get; set; }

        /// <inheritdoc cref="ITransportSettings.EnablePong" />
        public override bool EnablePong { get; set; } = true;

        /// <inheritdoc cref="ITransportSettings.TransportPrefix" />
        public override string TransportPrefix { get; set; } = "/";

        /// <inheritdoc cref="ITransportSettings.CallOperationEnabled" />
        public override bool CallOperationEnabled { get; set; } = true;

        /// <inheritdoc cref="ITransportSettings.CallOperationTimeout" />
        public override TimeSpan CallOperationTimeout { get; set; }

        /// <inheritdoc cref="ITransportSettings.CallOperationLimiterOptions" />
        public override TokenBucketRateLimiterOptions? CallOperationLimiterOptions { get; set; }

        /// <inheritdoc cref="ITransportSettings.InvokeOperationEnabled" />
        public override bool InvokeOperationEnabled { get; set; } = true;

        /// <inheritdoc cref="ITransportSettings.InvokeOperationTimeout" />
        public override TimeSpan InvokeOperationTimeout { get; set; }

        /// <inheritdoc cref="ITransportSettings.InvokeOperationLimiterOptions" />
        public override TokenBucketRateLimiterOptions? InvokeOperationLimiterOptions { get; set; }

        /// <inheritdoc cref="ITransportSettings.StreamOperationEnabled" />
        public override bool StreamOperationEnabled { get; set; } = true;

        /// <inheritdoc cref="ITransportSettings.StreamOperationTimeout" />
        public override TimeSpan StreamOperationTimeout { get; set; }

        /// <inheritdoc cref="ITransportSettings.StreamOperationLimiterOptions" />
        public override TokenBucketRateLimiterOptions? StreamOperationLimiterOptions { get; set; }

        /// <inheritdoc cref="ITransportSettings.IngestOperationEnabled" />
        public override bool IngestOperationEnabled { get; set; } = true;

        /// <inheritdoc cref="ITransportSettings.IngestOperationTimeout" />
        public override TimeSpan IngestOperationTimeout { get; set; }

        /// <inheritdoc cref="ITransportSettings.IngestOperationLimiterOptions" />
        public override TokenBucketRateLimiterOptions? IngestOperationLimiterOptions { get; set; }

        /// <inheritdoc cref="ITransportSettings.RetryableMessagesEnabled" />
        public override bool RetryableMessagesEnabled { get; set; }

        /// <inheritdoc cref="ITransportSettings.UseRateLimiters" />
        public override bool UseRateLimiters { get; set; } = true;

        /// <inheritdoc cref="ITransportSettings.LoggingEnabled" />
        public override bool LoggingEnabled { get; set; }

        /// <inheritdoc cref="ITransportSettings.AllowRemoteCancellation" />
        public override bool AllowRemoteCancellation { get; set; }

        /// <inheritdoc cref="ITransportSettings.TransportLimiterOptions" />
        public override TokenBucketRateLimiterOptions? TransportLimiterOptions { get; set; }

        /// <inheritdoc cref="ITransportSettings.MethodOverloadingEnabled" />
        public override bool MethodOverloadingEnabled { get; set; }

        /// <inheritdoc cref="ITransportSettings.MaxConcurrentRequestsPerIp" />
        public override int MaxConcurrentRequestsPerIp { get; set; } = 10;

        /// <inheritdoc cref="ITransportSettings.AllowAnonymousClients" />
        public override bool AllowAnonymousClients { get; set; } = true;

        /// <inheritdoc cref="ITransportSettings.TokenValidationParameters" />
        public override TokenValidationParameters? TokenValidationParameters { get; set; }

        /// <inheritdoc cref="ITransportSettings.CheckTokenExpirationOnMessageReceived" />
        public override bool CheckTokenExpirationOnMessageReceived { get; set; }

        /// <inheritdoc cref="ITransportSettings.ControlMessagesRateLimiterOptions" />
        public override TokenBucketRateLimiterOptions? ControlMessagesRateLimiterOptions { get; set; }

        /// <inheritdoc cref="ITransportSettings.ConnectionAuthHandlerType" />
        public override Type? ConnectionAuthHandlerType { get; set; }

        /// <inheritdoc cref="ITransportSettings.ConnectionTimeout" />
        public override TimeSpan ConnectionTimeout { get; set; }

        /// <inheritdoc cref="ITransportSettings.RequiresAuth" />
        public override bool RequiresAuth { get; set; } = true;
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
        /// <inheritdoc/>
        public override long MaxMessageSizeInBytes { get; set; } = 65535;

        /// <inheritdoc/>w
        public override TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <inheritdoc/>
        public override int MaxConnections { get; set; } = 5000;

        /// <inheritdoc/>
        public override int MaxConnectionsPerIp { get; set; } = 25;

        /// <inheritdoc/>
        public override bool EnablePing { get; set; } = true;

        /// <inheritdoc/>
        public override TokenBucketRateLimiterOptions? PingOperationLimiterOptions { get; set; }

        /// <inheritdoc/>
        public override bool EnablePong { get; set; } = true;

        /// <inheritdoc/>
        public override string TransportPrefix { get; set; } = "/ws";

        /// <inheritdoc/>
        public override bool CallOperationEnabled { get; set; } = true;

        /// <inheritdoc/>
        public override TimeSpan CallOperationTimeout { get; set; } = TimeSpan.FromSeconds(10);

        /// <inheritdoc/>
        public override TokenBucketRateLimiterOptions? CallOperationLimiterOptions { get; set; }

        /// <inheritdoc/>
        public override bool InvokeOperationEnabled { get; set; } = true;

        /// <inheritdoc/>
        public override TimeSpan InvokeOperationTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <inheritdoc/>
        public override TokenBucketRateLimiterOptions? InvokeOperationLimiterOptions { get; set; }

        /// <inheritdoc/>
        public override bool StreamOperationEnabled { get; set; } = true;

        /// <inheritdoc/>
        public override TimeSpan StreamOperationTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <inheritdoc/>
        public override TokenBucketRateLimiterOptions? StreamOperationLimiterOptions { get; set; }

        /// <inheritdoc/>
        public override bool IngestOperationEnabled { get; set; } = true;

        /// <inheritdoc/>
        public override TimeSpan IngestOperationTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <inheritdoc/>
        public override TokenBucketRateLimiterOptions? IngestOperationLimiterOptions { get; set; }

        /// <inheritdoc/>
        public override bool RetryableMessagesEnabled { get; set; }

        /// <inheritdoc/>
        public override bool UseRateLimiters { get; set; } = true;

        /// <inheritdoc/>
        public override bool LoggingEnabled { get; set; }

        /// <inheritdoc/>
        public override bool AllowRemoteCancellation { get; set; }

        /// <inheritdoc/>
        public override TokenBucketRateLimiterOptions? TransportLimiterOptions { get; set; }

        /// <inheritdoc/>
        public override bool MethodOverloadingEnabled { get; set; } = true;

        /// <inheritdoc/>
        public override int MaxConcurrentRequestsPerIp { get; set; } = 25;

        /// <inheritdoc/>
        public override bool AllowAnonymousClients { get; set; } = true;

        /// <inheritdoc/>
        public override TokenValidationParameters? TokenValidationParameters { get; set; }

        /// <inheritdoc/>
        public override bool CheckTokenExpirationOnMessageReceived { get; set; } = true;

        /// <inheritdoc/>
        public override TokenBucketRateLimiterOptions? ControlMessagesRateLimiterOptions { get; set; }

        /// <inheritdoc/>
        public override Type? ConnectionAuthHandlerType { get; set; }

        /// <inheritdoc/>
        public override TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromMinutes(60);

        /// <inheritdoc/>
        public override bool RequiresAuth { get; set; } = true;

        /// <summary>
        /// Determines the heartbeat expiration seconds. If the connection does not receive a ping in time, it may be aborted.
        /// </summary>
        public int HeartBeatInSeconds { get; set; } = 30;
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