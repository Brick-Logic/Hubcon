using System;
using System.Threading.RateLimiting;
using Microsoft.IdentityModel.Tokens;

namespace Hubcon
{
    /// <inheritdoc/>
    public sealed class TransportSettings : ITransportSettings
    {
        /// <inheritdoc/>
        public long MaxMessageSizeInBytes { get; set; } = 65535;
        
        /// <inheritdoc/>
        public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(15);
        
        /// <inheritdoc/>
        public int MaxConnections { get; set; } = 1000;
        
        /// <inheritdoc/>
        public int MaxConnectionsPerIp { get; set; } = 10;
        
        /// <inheritdoc/>
        public bool EnablePing { get; set; } = true;
        
        /// <inheritdoc/>
        public TokenBucketRateLimiterOptions? PingOperationLimiterOptions { get; set; }
        
        /// <inheritdoc/>
        public bool EnablePong { get; set; } = true;
        
        /// <inheritdoc/>
        public string TransportPrefix { get; set; } = "/";
        
        /// <inheritdoc/>
        public bool CallOperationEnabled { get; set; } = true;
        
        /// <inheritdoc/>
        public TimeSpan CallOperationTimeout { get; set; }
        
        /// <inheritdoc/>
        public TokenBucketRateLimiterOptions? CallOperationLimiterOptions { get; set; }
        
        /// <inheritdoc/>
        public bool InvokeOperationEnabled { get; set; } = true;
        
        /// <inheritdoc/>
        public TimeSpan InvokeOperationTimeout { get; set; }
        
        /// <inheritdoc/>
        public TokenBucketRateLimiterOptions? InvokeOperationLimiterOptions { get; set; }
        
        /// <inheritdoc/>
        public bool StreamOperationEnabled { get; set; } = true;
        
        /// <inheritdoc/>
        public TimeSpan StreamOperationTimeout { get; set; }
        
        /// <inheritdoc/>
        public TokenBucketRateLimiterOptions? StreamOperationLimiterOptions { get; set; }
        
        /// <inheritdoc/>
        public bool IngestOperationEnabled { get; set; } = true;
        
        /// <inheritdoc/>
        public TimeSpan IngestOperationTimeout { get; set; }
        
        /// <inheritdoc/>
        public TokenBucketRateLimiterOptions? IngestOperationLimiterOptions { get; set; }
        
        /// <inheritdoc/>
        public bool RetryableMessagesEnabled { get; set; }
        
        /// <inheritdoc/>
        public bool UseRateLimiters { get; set; } = true;
        
        /// <inheritdoc/>
        public bool LoggingEnabled { get; set; }
        
        /// <inheritdoc/>
        public bool AllowRemoteCancellation { get; set; }
        
        /// <inheritdoc/>
        public TokenBucketRateLimiterOptions? TransportLimiterOptions { get; set; }
        
        /// <inheritdoc/>
        public bool MethodOverloadingEnabled { get; set; }
        
        /// <inheritdoc/>
        public int MaxConcurrentRequestsPerIp { get; set; } = 10;
        
        /// <inheritdoc/>
        public bool AllowAnonymousClients { get; set; } = true;
        
        /// <inheritdoc/>
        public TokenValidationParameters? TokenValidationParameters { get; set; }
        
        /// <inheritdoc/>
        public bool CheckTokenExpirationOnMessageReceived { get; set; }
        
        /// <inheritdoc/>
        public TokenBucketRateLimiterOptions? ControlMessagesRateLimiterOptions { get; set; }

        /// <inheritdoc/>
        public Type? ConnectionAuthHandlerType { get; set; }

        /// <inheritdoc/>
        public TimeSpan ConnectionTimeout { get; set; }

        /// <inheritdoc/>
        public bool RequiresAuth { get; set; } = true;


        /// <summary>
        /// Creates a new instance with the same property values.
        /// </summary>
        /// <returns></returns>
        public TransportSettings GetCopy()
        {
            return new TransportSettings()
            {
                MaxMessageSizeInBytes = MaxMessageSizeInBytes,
                RequestTimeout = RequestTimeout,
                MaxConnections = MaxConnections,
                MaxConnectionsPerIp = MaxConnectionsPerIp,
                EnablePing = EnablePing,
                PingOperationLimiterOptions = PingOperationLimiterOptions,
                EnablePong = EnablePong,
                TransportPrefix = TransportPrefix,
                CallOperationEnabled = CallOperationEnabled,
                CallOperationTimeout = CallOperationTimeout,
                CallOperationLimiterOptions = CallOperationLimiterOptions,
                InvokeOperationEnabled = InvokeOperationEnabled,
                InvokeOperationTimeout = InvokeOperationTimeout,
                InvokeOperationLimiterOptions = InvokeOperationLimiterOptions,
                StreamOperationEnabled = StreamOperationEnabled,
                StreamOperationTimeout = StreamOperationTimeout,
                StreamOperationLimiterOptions = StreamOperationLimiterOptions,
                IngestOperationEnabled = IngestOperationEnabled,
                IngestOperationTimeout = IngestOperationTimeout,
                IngestOperationLimiterOptions = IngestOperationLimiterOptions,
                RetryableMessagesEnabled = RetryableMessagesEnabled,
                UseRateLimiters = UseRateLimiters,
                LoggingEnabled = LoggingEnabled,
                AllowRemoteCancellation = AllowRemoteCancellation,
                TransportLimiterOptions = TransportLimiterOptions,
                MethodOverloadingEnabled = MethodOverloadingEnabled,
                MaxConcurrentRequestsPerIp = MaxConcurrentRequestsPerIp,
                AllowAnonymousClients = AllowAnonymousClients,
                TokenValidationParameters = TokenValidationParameters,
                CheckTokenExpirationOnMessageReceived = CheckTokenExpirationOnMessageReceived,
                ControlMessagesRateLimiterOptions = ControlMessagesRateLimiterOptions,
                ConnectionAuthHandlerType = ConnectionAuthHandlerType,
                ConnectionTimeout = ConnectionTimeout,
                RequiresAuth = RequiresAuth
            };
        }
    }
}