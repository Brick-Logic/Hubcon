using Hubcon.Shared.Abstractions.Enums;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Abstractions.Standard.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Net.WebSockets;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

namespace Hubcon.Client.Abstractions.Interfaces
{
    /// <summary>
    /// Defines the builder contract for configuring and initializing a Hubcon client.
    /// This interface manages the transition from configuration properties to active 
    /// service contract proxies and authentication lifecycle.
    /// </summary>
    public interface IClientBuilder
    {
        #region Configuration Properties
        /// <summary>Gets or sets the type of the authentication manager.</summary>
        Type? AuthenticationManagerType { get; set; }

        /// <summary>Gets or sets the base address of the remote Hubcon server.</summary>
        Uri? BaseUri { get; set; }

        /// <summary>Gets the list of service contract types registered with this client.</summary>
        List<Type> Contracts { get; set; }

        /// <summary>Gets or sets the URL prefix for HTTP operations.</summary>
        string? HttpPrefix { get; set; }

        /// <summary>Gets or sets a value indicating whether SSL/TLS is required.</summary>
        bool UseSecureConnection { get; set; }

        /// <summary>Gets or sets the URL prefix for WebSocket operations.</summary>
        string? WebsocketPrefix { get; set; }

        /// <summary>Gets or sets a delegate to configure underlying <see cref="ClientWebSocketOptions"/>.</summary>
        Action<ClientWebSocketOptions, IServiceProvider>? WebSocketOptions { get; set; }

        /// <summary>Gets or sets a delegate to configure the underlying <see cref="HttpClient"/>.</summary>
        Action<HttpClient, IServiceProvider>? HttpClientOptions { get; set; }

        /// <summary>Gets or sets the interval for sending WebSocket pings.</summary>
        TimeSpan WebsocketPingInterval { get; set; }
        
        /// <summary>Gets the WebSocket keep-alive pongs time.</summary>
        TimeSpan WebsocketPongTime { get; set; }

        /// <summary>Gets or sets a value indicating whether the client expects pongs from the server.</summary>
        bool WebsocketRequiresPong { get; set; }

        /// <summary>Gets or sets the number of concurrent message processing tasks.</summary>
        int MessageProcessorsCount { get; set; }

        /// <summary>Gets or sets whether the client should automatically reconnect on transport failure.</summary>
        bool AutoReconnect { get; set; }

        /// <summary>Gets or sets whether streams should automatically resume after a reconnection.</summary>
        bool ReconnectStreams { get; set; }

        /// <summary>Gets or sets whether subscriptions should automatically resume after a reconnection.</summary>
        bool ReconnectSubscriptions { get; set; }

        /// <summary>Gets or sets whether active ingestion flows should resume after a reconnection.</summary>
        bool ReconnectIngests { get; set; }

        /// <summary>Gets or sets the timeout duration for WebSocket operations.</summary>
        TimeSpan WebsocketTimeout { get; set; }

        /// <summary>Gets or sets the timeout duration for HTTP operations.</summary>
        TimeSpan HttpTimeout { get; set; }

        /// <summary>Gets the global rate limiter instance.</summary>
        public RateLimiter? RateBucket { get; }

        /// <summary>Gets or sets the global rate limiting policy options.</summary>
        TokenBucketRateLimiterOptions? RateBucketOptions { get; set; }

        /// <summary>Gets or sets whether rate limiters are bypassed for this client.</summary>
        bool LimitersDisabled { get; set; }
        #endregion

#pragma warning disable CS1591
        #region Specialized Limiter Options
        public TokenBucketRateLimiterOptions? IngestLimiterOptions { get; set; }
        public TokenBucketRateLimiterOptions? SubscriptionLimiterOptions { get; set; }
        public TokenBucketRateLimiterOptions? StreamingLimiterOptions { get; set; }
        public TokenBucketRateLimiterOptions? WebsocketRoundTripLimiterOptions { get; set; }
        public TokenBucketRateLimiterOptions? HttpRoundTripLimiterOptions { get; set; }
        public TokenBucketRateLimiterOptions? WebsocketFireAndForgetLimiterOptions { get; set; }
        public TokenBucketRateLimiterOptions? HttpFireAndForgetLimiterOptions { get; set; }
        #endregion
#pragma warning restore CS1591

        /// <summary>Gets or sets whether diagnostic logging is enabled.</summary>
        bool LoggingEnabled { get; set; }

        /// <summary>Gets or sets whether authentication is enabled for this client.</summary>
        bool AuthIsEnabled { get; set; }

        /// <summary>Gets or sets the factory used to create the internal <see cref="HttpClient"/>.</summary>
        Func<IServiceProvider, HttpClient> HttpClientFactory { get; set; }

        /// <summary>Gets or sets the default transport mode (e.g., WebSocket) for the client.</summary>
        HubconTransportAttribute TransportType { get; set; }

        /// <summary>Gets the collection of dynamic header providers.</summary>
        Dictionary<string, Func<IServiceProvider, string>> HeaderProviders { get; }

        /// <summary>
        /// Adds a setting to the framework.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        void AddSetting(string key, object? value);

        #region Client Generation & Management
        /// <summary>
        /// Resolves or creates a strongly-typed proxy instance for a service contract.
        /// </summary>
        T GetOrCreateClient<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(IServiceProvider services, bool useCached = true) where T : IControllerContract;

        /// <summary>
        /// Resolves or creates a proxy instance for a service contract based on the provided type.
        /// </summary>
        object GetOrCreateClient([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type contractType, IServiceProvider services, bool useCached = true);

        /// <summary>
        /// Registers the proxy generation logic for a contract into the service collection.
        /// </summary>
        void LoadContractProxy([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type contractType, IServiceCollection services);
        #endregion

        #region Authentication
        /// <summary>Registers the authentication manager type and its associated services.</summary>
        void UseAuthenticationManager<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T>(IServiceCollection services) where T : class, IAuthenticationManager;

        /// <summary>Registers the authentication manager with specific token refresh timing settings.</summary>
        void UseAuthenticationManager<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T>(IServiceCollection services, TimeSpan refreshBeforeExpirationMargin, TimeSpan refreshCheckInterval) where T : class, IAuthenticationManager;
        #endregion

        #region Fluent Configuration
        /// <summary>Provides a fluent interface to configure specific behavior for a service contract.</summary>
        void ConfigureContract<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T>(Action<IContractConfigurator<T>>? configure) where T : IControllerContract;

        /// <summary>Adds a global interceptor that triggers during specific client-side lifecycle events.</summary>
        void AddInterceptor(InterceptorType interceptorType, Func<IInvocationContext, Task> interceptorDelegate);

        /// <summary>Enables support for mapping multiple HTTP methods to a single endpoint signature.</summary>
        void EnableHttpEndpointOverloading();

        /// <summary>Sets whether client-side cancellation should be transmitted to the server.</summary>
        void AllowRemoteCancellation(bool allowed);
        #endregion

        /// <summary>
        /// Adds tracing support for this client.
        /// </summary>
        /// <param name="shouldTrace"></param>
        void AddTracing(bool shouldTrace = true);
    }
}