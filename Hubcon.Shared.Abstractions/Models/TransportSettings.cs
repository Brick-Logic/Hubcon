using System;
using System.Threading.RateLimiting;
using Hubcon.Shared.Abstractions.Interfaces;
using Microsoft.IdentityModel.Tokens;

namespace Hubcon
{
    /// <inheritdoc />
    public class TransportSettings : ISettableTransportSettings
    {
        /// <inheritdoc />
        public virtual long MaxMessageSizeInBytes { get; set; } = 65535;

        /// <inheritdoc />
        public virtual TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(15);

        /// <inheritdoc />
        public virtual int MaxConnections { get; set; } = 1000;

        /// <inheritdoc />
        public virtual int MaxConnectionsPerIp { get; set; } = 10;

        /// <inheritdoc />
        public virtual bool EnablePing { get; set; } = true;

        /// <inheritdoc />
        public int PingOperationLimitPerSecond { get; set; } = 999_999;

        /// <inheritdoc />
        public virtual bool EnablePong { get; set; } = true;

        /// <inheritdoc />
        public virtual string TransportPrefix { get; set; } = "/";

        /// <inheritdoc />
        public virtual bool CallOperationEnabled { get; set; } = true;

        /// <inheritdoc />
        public virtual TimeSpan CallOperationTimeout { get; set; } = TimeSpan.FromSeconds(15);

        /// <inheritdoc />
        public int CallOperationLimitPerSecond { get; set; } = 999_999;

        /// <inheritdoc />
        public virtual bool InvokeOperationEnabled { get; set; } = true;

        /// <inheritdoc />
        public virtual TimeSpan InvokeOperationTimeout { get; set; } = TimeSpan.FromSeconds(15);

        /// <inheritdoc />
        public int InvokeOperationLimitPerSecond { get; set; } = 999_999;

        /// <inheritdoc />
        public virtual bool StreamOperationEnabled { get; set; } = true;

        /// <inheritdoc />
        public virtual TimeSpan StreamOperationTimeout { get; set; } = TimeSpan.FromSeconds(15);

        /// <inheritdoc />
        public int StreamOperationLimitPerSecond { get; set; } = 999_999;

        /// <inheritdoc />
        public virtual bool IngestOperationEnabled { get; set; } = true;

        /// <inheritdoc />
        public virtual TimeSpan IngestOperationTimeout { get; set; } = TimeSpan.FromSeconds(15);

        /// <inheritdoc />
        public int IngestOperationLimitPerSecond { get; set; } = 999_999;

        /// <inheritdoc />
        public int ControlMessagesLimitPerSecond { get; set; } = 999_999;

        /// <inheritdoc />
        public int ControlMessagesPerSecond { get; set; } = 999_999;

        /// <inheritdoc />
        public virtual bool RetryableMessagesEnabled { get; set; } = false;

        /// <inheritdoc />
        public virtual bool UseRateLimiters { get; set; } = true;

        /// <inheritdoc />
        public virtual bool LoggingEnabled { get; set; }

        /// <inheritdoc />
        public virtual bool AllowRemoteCancellation { get; set; }

        /// <inheritdoc />
        public int TransportLimitPerSecond { get; set; } = 999_999;

        /// <inheritdoc />
        public IRateLimitAuthority? TransportRateLimitAuthority { get; set; }

        /// <inheritdoc />
        public virtual bool MethodOverloadingEnabled { get; set; }

        /// <inheritdoc />
        public virtual int MaxConcurrentRequestsPerIp { get; set; } = 10;

        /// <inheritdoc />
        public virtual bool AllowAnonymousClients { get; set; } = false;

        /// <inheritdoc />
        public virtual TokenValidationParameters? TokenValidationParameters { get; set; }

        /// <inheritdoc />
        public virtual bool CheckTokenExpirationOnMessageReceived { get; set; }

        /// <inheritdoc />
        public virtual Type? ConnectionAuthHandlerType { get; set; }

        public void UseAuthHandler<T>() where T : class, IHubconAuthHandler
        {
            ConnectionAuthHandlerType ??= typeof(T);
        }

        public void UseTokenValidationParameters(TokenValidationParameters tokenValidationParameters)
        {
            TokenValidationParameters ??= tokenValidationParameters;
        }

        /// <inheritdoc />
        public virtual TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(15);

        /// <inheritdoc />
        public virtual bool RequiresAuth { get; set; } = true;
    }
    
    /// <inheritdoc />
    public class ClientSettings : ISettableClientSettings
    {
    }
}