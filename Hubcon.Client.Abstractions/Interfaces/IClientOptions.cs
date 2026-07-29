using Hubcon.Shared.Abstractions.Enums;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.WebSockets;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

namespace Hubcon.Client.Abstractions.Interfaces
{
    /// <summary>
    /// Defines the global configuration options for a Hubcon client.
    /// Manages connection lifecycle, protocol-specific settings, rate limiting, and contract registration.
    /// </summary>
    public interface IClientOptions
    {
        /// <summary>
        /// Externally added settings.
        /// </summary>
        IReadOnlyDictionary<string, object?> ExternalSettings { get; }
        
        /// <summary>
        /// Determines if tracing is enabled.
        /// </summary>
        public bool? TracingEnabled { get; }
        
        /// <summary>Gets the base URI of the Hubcon server.</summary>
        public Uri? BaseUri { get; }

        /// <summary>Gets the list of service contract interfaces registered for this client.</summary>
        public List<Type> Contracts { get; }

        /// <summary>Gets the type of the <see cref="IAuthenticationManager"/> used for secure communication.</summary>
        public Type? AuthenticationManagerType { get; }

        /// <summary>Gets or sets the factory for resolving the authentication manager via dependency injection.</summary>
        ILazyWrapper AuthenticationManagerFactory { get; set; }

        /// <summary>Gets the URL prefix for HTTP-based operations.</summary>
        public string? HttpPrefix { get; }

        /// <summary>Gets the URL prefix for WebSocket-based operations.</summary>
        public string? WebsocketPrefix { get; }

        /// <summary>Gets the configuration delegate for underlying <see cref="ClientWebSocketOptions"/>.</summary>
        public Action<ClientWebSocketOptions, IServiceProvider>? WebSocketOptions { get; }

        /// <summary>Gets the configuration delegate for the underlying <see cref="HttpClient"/>.</summary>
        public Action<HttpClient, IServiceProvider>? HttpClientOptions { get; }

        /// <summary>Gets a value indicating whether SSL/TLS (HTTPS/WSS) is enforced.</summary>
        public bool UseSecureConnection { get; }

        /// <summary>Gets the interval for sending WebSocket keep-alive pings.</summary>
        TimeSpan WebsocketPingInterval { get; }

        /// <summary>Gets a value indicating whether the client expects a pong response for every ping sent.</summary>
        bool WebsocketRequiresPong { get; }

        /// <summary>Gets the number of concurrent message processors for handling incoming data.</summary>
        int MessageProcessorsCount { get; }

        /// <summary>Gets a value indicating whether the client should automatically attempt to reconnect on failure.</summary>
        bool AutoReconnect { get; }

        /// <summary>Gets a value indicating whether active streams should be resumed upon reconnection.</summary>
        bool ReconnectStreams { get; }

        /// <summary>Gets a value indicating whether active subscriptions should be resumed upon reconnection.</summary>
        bool ReconnectSubscriptions { get; }

        /// <summary>Gets a value indicating whether active ingestion processes should be resumed upon reconnection.</summary>
        bool ReconnectIngests { get; }

        /// <summary>Gets the timeout duration for WebSocket handshakes and operations.</summary>
        TimeSpan WebsocketTimeout { get; }

        /// <summary>Gets the timeout duration for HTTP requests.</summary>
        TimeSpan HttpTimeout { get; }

        /// <summary>Gets the global rate limiter instance for the client.</summary>
        RateLimiter? RateBucket { get; }

        /// <summary>Gets the global rate limiting configuration options.</summary>
        TokenBucketRateLimiterOptions? RateBucketOptions { get; }

        /// <summary>Gets a value indicating whether all rate limiters are bypassed.</summary>
        bool LimitersDisabled { get; }

        /// <summary>Gets the name of the server-side module this client is targeting.</summary>
        public string ServerModuleName { get; }

#pragma warning disable CS1591
        #region Rate Limiter Options
        public TokenBucketRateLimiterOptions? IngestLimiterOptions { get; }
        public TokenBucketRateLimiterOptions? SubscriptionLimiterOptions { get; }
        public TokenBucketRateLimiterOptions? StreamingLimiterOptions { get; }
        public TokenBucketRateLimiterOptions? WebsocketRoundTripLimiterOptions { get; }
        public TokenBucketRateLimiterOptions? HttpRoundTripLimiterOptions { get; }
        public TokenBucketRateLimiterOptions? WebsocketFireAndForgetLimiterOptions { get; }
        public TokenBucketRateLimiterOptions? HttpFireAndForgetLimiterOptions { get; }
        #endregion

        #region Active Rate Buckets
        RateLimiter? IngestRateBucket { get; }
        RateLimiter? SubscriptionRateBucket { get; }
        RateLimiter? StreamingRateBucket { get; }
        RateLimiter? WebsocketRoundTripRateBucket { get; }
        RateLimiter? HttpRoundTripRateBucket { get; }
        RateLimiter? WebsocketFireAndForgetRateBucket { get; }
        RateLimiter? HttpFireAndForgetRateBucket { get; }
        #endregion
#pragma warning restore CS1591

        /// <summary>Gets a value indicating whether diagnostic logging is enabled for the client.</summary>
        bool LoggingEnabled { get; }

        /// <summary>Gets a value indicating whether authentication logic is active.</summary>
        bool AuthIsEnabled { get; }

        /// <summary>Gets a value indicating whether multiple HTTP methods are allowed to map to the same endpoint.</summary>
        bool UseHttpEndpointOverloading { get; }

        /// <summary>Gets the factory used to construct the internal <see cref="HttpClient"/>.</summary>
        Func<IServiceProvider, HttpClient> HttpClientFactory { get; }

        /// <summary>
        /// Retrieves the specific configuration options for a given contract type.
        /// </summary>
        IContractOptions GetContractOptions(Type type);

        /// <summary>
        /// Asynchronously triggers a client-level interceptor for a specific lifecycle event.
        /// </summary>
        Task CallInterceptor(InterceptorType interceptorType, IInvocationContext context);

        /// <summary>Gets the default transport attribute (WebSocket/HTTP) for this client.</summary>
        HubconTransportAttribute TransportType { get; }

        /// <summary>Gets the dictionary of dynamic header providers.</summary>
        Dictionary<string, Func<IServiceProvider, string>> HeaderProviders { get; }

        /// <summary>Gets a value indicating whether client cancellation tokens are transmitted to the server.</summary>
        bool RemoteCancellationIsAllowed { get; }
    }
}