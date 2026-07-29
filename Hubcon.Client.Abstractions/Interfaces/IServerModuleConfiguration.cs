using Hubcon.Shared.Abstractions.Enums;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Abstractions.Standard.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Net.WebSockets;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

namespace Hubcon
{
    /// <summary>
    /// The server module configurator.
    /// </summary>
    public interface IServerModuleConfiguration
    {
        /// <summary>
        /// Registers a contract interface with optional configuration. 
        /// <br></br>
        /// This method will extract and use the configuration attributes from the type <typeparamref name="T"/> and automatically allow it to be injected directly through dependency injection.
        /// </summary>
        /// <typeparam name="T">The controller contract type.</typeparam>
        /// <param name="configure">Optional configuration action for the contract.</param>
        /// <returns>The current instance of <see cref="IServerModuleConfiguration"/> for method chaining.</returns>
        IServerModuleConfiguration Implements<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T>(Action<IContractConfigurator<T>>? configure = null) where T : IControllerContract;

        /// <summary>
        /// Specifies the authentication manager to use for the server module. The authentication manager will only be used for this module.
        /// </summary>
        /// <typeparam name="T">The authentication manager type.</typeparam>
        /// <returns>The current instance of <see cref="IServerModuleConfiguration"/> for method chaining.</returns>
        IServerModuleConfiguration UseAuthenticationManager<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T>() where T : class, IAuthenticationManager;

        /// <summary>
        /// Specifies the authentication manager to use for the server module. The authentication manager will only be used for this module.
        /// </summary>
        /// <typeparam name="T">The authentication manager type.</typeparam>
        /// <returns>The current instance of <see cref="IServerModuleConfiguration"/> for method chaining.</returns>
        IServerModuleConfiguration UseAuthenticationManager<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T>(TimeSpan refreshBeforeExpirationMargin, TimeSpan refreshCheckInterval) where T : class, IAuthenticationManager;

        /// <summary>
        /// Sets the base URL that the contracts will use to connect to the server.
        /// </summary>
        /// <param name="baseUrl">The base URL string.</param>
        /// <returns>The current instance of <see cref="IServerModuleConfiguration"/> for method chaining.</returns>
        IServerModuleConfiguration WithBaseUrl(string baseUrl);

        /// <summary>
        /// Configures the server module to use an insecure connection (e.g., HTTP instead of HTTPS, WS instead of WSS). Testing only, not intended or recommended for production.
        /// </summary>
        /// <returns>The current instance of <see cref="IServerModuleConfiguration"/> for method chaining.</returns>
        IServerModuleConfiguration UseInsecureConnection();

        /// <summary>
        /// Sets the HTTP prefix for requests.
        /// </summary>
        /// <param name="prefix">The HTTP prefix string.</param>
        /// <returns>The current instance of <see cref="IServerModuleConfiguration"/> for method chaining.</returns>
        IServerModuleConfiguration WithHttpPrefix(string prefix);

        /// <summary>
        /// Sets the WebSocket endpoint that the client should use to connect to the server.
        /// </summary>
        /// <param name="endpoint">The WebSocket endpoint string.</param>
        /// <returns>The current instance of <see cref="IServerModuleConfiguration"/> for method chaining.</returns>
        IServerModuleConfiguration WithWebsocketEndpoint(string endpoint);

        /// <summary>
        /// Configures the WebSocket client options.
        /// </summary>
        /// <param name="options">An action to configure <see cref="ClientWebSocketOptions"/>.</param>
        /// <returns>The current instance of <see cref="IServerModuleConfiguration"/> for method chaining.</returns>
        IServerModuleConfiguration ConfigureWebsocketClient(Action<ClientWebSocketOptions, IServiceProvider> options);

        /// <summary>
        /// Configures the HTTP client options.
        /// </summary>
        /// <param name="options">An action to configure <see cref="HttpClient"/>.</param>
        /// <returns>The current instance of <see cref="IServerModuleConfiguration"/> for method chaining.</returns>
        IServerModuleConfiguration ConfigureHttpClient(Action<HttpClient, IServiceProvider> options);

        /// <summary>
        /// Sets the interval for sending WebSocket ping messages.
        /// </summary>
        /// <param name="timeSpan">The ping interval.</param>
        /// <returns>The current instance of <see cref="IServerModuleConfiguration"/> for method chaining.</returns>
        IServerModuleConfiguration SetWebsocketPingInterval(TimeSpan timeSpan);

        /// <summary>
        /// Specifies whether a pong response is required for WebSocket pings. True by default.
        /// </summary>
        /// <param name="value">True to require pong response; otherwise, false.</param>
        /// <returns>The current instance of <see cref="IServerModuleConfiguration"/> for method chaining.</returns>
        IServerModuleConfiguration RequirePongResponse(bool value);

        /// <summary>
        /// Disabled. This feature has no effect.
        /// Sets the number of message processors to scale to. The message processors are used to handle incoming messages. 
        /// Scaling them allows for better performance and concurrency in processing messages. 
        /// Do not set this value too high, as it may lead to performance degradation.
        /// Default value is 1.
        /// </summary>
        /// <param name="count">The number of processors.</param>
        /// <returns>The current instance of <see cref="IServerModuleConfiguration"/> for method chaining.</returns>
        IServerModuleConfiguration ScaleMessageProcessors(int count);

        /// <summary>
        /// Enables or disables automatic websocket reconnection for the server module. Note that any operation that makes use of Websockets will trigger a reconnection.
        /// </summary>
        /// <param name="value">True to enable auto reconnect; otherwise, false. Default is true.</param>
        /// <returns>The current instance of <see cref="IServerModuleConfiguration"/> for method chaining.</returns>
        IServerModuleConfiguration EnableWebsocketAutoReconnect(bool value = true);

        /// <summary>
        /// Enables or disables automatic reconnection for stream connections. Note: This feature is not implemented.
        /// </summary>
        /// <param name="value">True to enable reconnecting streams; otherwise, false. Default is true.</param>
        /// <returns>The current instance of <see cref="IServerModuleConfiguration"/> for method chaining.</returns>
        IServerModuleConfiguration ResubcribeStreamingOnReconnect(bool value = true);

        ///// <summary>
        ///// Enables or disables automatic reconnection for subscriptions. Note: This feature is not implemented, 
        ///// but subscriptions based on ISubscription<T> will automatically be reconnected via request resend, 1 per subscription.
        ///// </summary>
        ///// <param name="value">True to enable reconnecting subscriptions; otherwise, false. Default is true.</param>
        ///// <returns>The current server module configuration instance.</returns>
        //IServerModuleConfiguration ResubscribeOnReconnect(bool value = true);

        /// <summary>
        /// Enables or disables automatic reconnection for ingest connections. Note: This feature is not implemented.
        /// </summary>
        /// <param name="value">True to enable reconnecting ingests; otherwise, false. Default is true.</param>
        /// <returns>The current instance of <see cref="IServerModuleConfiguration"/> for method chaining.</returns>
        IServerModuleConfiguration ResubscribeIngestOnReconnect(bool value = true);

        /// <summary>
        /// Disables all rate limiters.
        /// </summary>
        /// <returns>The current instance of <see cref="IServerModuleConfiguration"/> for method chaining.</returns>
        IServerModuleConfiguration DisableAllLimiters();

        /// <summary>
        /// Enables a rate limiter for this server.
        /// If <paramref name="messagesPerSecond"/> is 0, it sets a default high limit of 9,999,999.
        /// </summary>
        /// <returns>The current instance of <see cref="IServerModuleConfiguration"/> for method chaining.</returns>
        IServerModuleConfiguration GlobalLimit(int messagesPerSecond);

        /// <summary>
        /// Sets a rate limit for ingest messages (messages sent to the server).
        /// If <paramref name="messagesPerSecond"/> is 0, it sets a default high limit of 9,999,999.
        /// </summary>
        /// <returns>The current instance of <see cref="IServerModuleConfiguration"/> for method chaining.</returns>
        IServerModuleConfiguration LimitIngest(int messagesPerSecond);

        ///// <summary>
        ///// Sets a rate limit for subscription messages (client-side subscriptions).
        ///// If <paramref name="messagesPerSecond"/> is 0, it sets a default high limit of 9,999,999.
        ///// </summary>
        ///// <returns>The current <see cref="IServerModuleConfiguration"/> instance, allowing for method chaining.</returns>
        //IServerModuleConfiguration LimitSubscription(int messagesPerSecond);

        /// <summary>
        /// Sets a rate limit for streaming messages (data streaming from client to server).
        /// If <paramref name="messagesPerSecond"/> is 0, it sets a default high limit of 9,999,999.
        /// </summary>
        /// <returns>The current instance of <see cref="IServerModuleConfiguration"/> for method chaining.</returns>
        IServerModuleConfiguration LimitStreaming(int messagesPerSecond);

        /// <summary>
        /// Sets a rate limit for round-trip messages over WebSocket (request-response).
        /// If <paramref name="messagesPerSecond"/> is 0, it sets a default high limit of 9,999,999.
        /// </summary>
        /// <returns>The current instance of <see cref="IServerModuleConfiguration"/> for method chaining.</returns>
        IServerModuleConfiguration LimitWebsocketRoundTrip(int messagesPerSecond);

        /// <summary>
        /// Sets a rate limit for round-trip messages over HTTP (request-response).
        /// If <paramref name="messagesPerSecond"/> is 0, it sets a default high limit of 9,999,999.
        /// </summary>
        /// <returns>The current instance of <see cref="IServerModuleConfiguration"/> for method chaining.</returns>
        IServerModuleConfiguration LimitHttpRoundTrip(int messagesPerSecond);

        /// <summary>
        /// Sets a rate limit for fire-and-forget messages over WebSocket (no response).
        /// If <paramref name="messagesPerSecond"/> is 0, it sets a default high limit of 9,999,999.
        /// </summary>
        /// <returns>The current instance of <see cref="IServerModuleConfiguration"/> for method chaining.</returns>
        IServerModuleConfiguration LimitWebsocketFireAndForget(int messagesPerSecond);

        /// <summary>
        /// Sets a rate limit for fire-and-forget messages over HTTP (no response).
        /// If <paramref name="messagesPerSecond"/> is 0, it sets a default high limit of 9,999,999.
        /// </summary>
        /// <returns>The current instance of <see cref="IServerModuleConfiguration"/> for method chaining.</returns>
        IServerModuleConfiguration LimitHttpFireAndForget(int messagesPerSecond);

        /// <summary>
        /// Enables a rate limiter for this server.
        /// If <paramref name="options"/> is null, the rate limiter will be disabled.
        /// </summary>
        /// <returns>The current instance of <see cref="IServerModuleConfiguration"/> for method chaining.</returns>
        IServerModuleConfiguration GlobalLimit(TokenBucketRateLimiterOptions? options);

        /// <summary>
        /// Sets a rate limit for ingest messages (messages sent to the server).
        /// If <paramref name="options"/> is null, the rate limiter will be disabled.
        /// </summary>
        /// <returns>The current instance of <see cref="IServerModuleConfiguration"/> for method chaining.</returns>
        IServerModuleConfiguration LimitIngest(TokenBucketRateLimiterOptions? options);

        ///// <summary>
        ///// Sets a rate limit for subscription messages (client-side subscriptions).
        ///// If <paramref name="options"/> is null, the rate limiter will be disabled.
        ///// </summary>
        ///// <returns>The current <see cref="IServerModuleConfiguration"/> instance, allowing for method chaining.</returns>
        //IServerModuleConfiguration LimitSubscription(TokenBucketRateLimiterOptions? options);

        /// <summary>
        /// Sets a rate limit for streaming messages (data streaming from client to server).
        /// If <paramref name="options"/> is null, the rate limiter will be disabled.
        /// </summary>
        /// <returns>The current instance of <see cref="IServerModuleConfiguration"/> for method chaining.</returns>
        IServerModuleConfiguration LimitStreaming(TokenBucketRateLimiterOptions? options);

        /// <summary>
        /// Sets a rate limit for round-trip messages over WebSocket (request-response).
        /// If <paramref name="options"/> is null, the rate limiter will be disabled.
        /// </summary>
        /// <returns>The current instance of <see cref="IServerModuleConfiguration"/> for method chaining.</returns>
        IServerModuleConfiguration LimitWebsocketRoundTrip(TokenBucketRateLimiterOptions? options);

        /// <summary>
        /// Sets a rate limit for fire-and-forget messages over WebSocket (no response).
        /// If <paramref name="options"/> is null, the rate limiter will be disabled.
        /// </summary>
        /// <returns>The current instance of <see cref="IServerModuleConfiguration"/> for method chaining.</returns>
        IServerModuleConfiguration LimitWebsocketFireAndForget(TokenBucketRateLimiterOptions? options);

        /// <summary>
        /// Sets a rate limit for round-trip messages over HTTP (request-response).
        /// If <paramref name="options"/> is null, the rate limiter will be disabled.
        /// </summary>
        /// <returns>The current instance of <see cref="IServerModuleConfiguration"/> for method chaining.</returns>
        IServerModuleConfiguration LimitHttpRoundTrip(TokenBucketRateLimiterOptions? options);

        /// <summary>
        /// Sets a rate limit for fire-and-forget messages over HTTP (no response).
        /// If <paramref name="options"/> is null, the rate limiter will be disabled.
        /// </summary>
        /// <returns>The current instance of <see cref="IServerModuleConfiguration"/> for method chaining.</returns>
        IServerModuleConfiguration LimitHttpFireAndForget(TokenBucketRateLimiterOptions? options);

        /// <summary>
        /// Enables logging for this server module. Logging is disabled by default.
        /// </summary>
        /// <returns>The current instance of <see cref="IServerModuleConfiguration"/> for method chaining.</returns>
        IServerModuleConfiguration EnableLogging();

        /// <summary>
        /// Disables HTTP authentication for the server module.
        /// </summary>
        /// <remarks>This method configures the server module to operate without requiring HTTP
        /// authentication.  Use this method when authentication is not needed or is handled by other
        /// mechanisms.</remarks>
        /// <returns>The current instance of <see cref="IServerModuleConfiguration"/> for method chaining.</returns>
        IServerModuleConfiguration AuthIsEnabled(bool enabled = true);

        /// <summary>
        /// Adds an interceptor to be triggered during the operation lifecycle. Note that the same interceptor type cannot be registered multiple times.
        /// </summary>
        /// <param name="interceptorType">The type of hook to add, specifying when it should be triggered.</param>
        /// <param name="interceptorDelegate">The delegate to execute when the hook is triggered.</param>
        /// <returns>The current instance of <see cref="IServerModuleConfiguration"/> for method chaining.</returns>
        IServerModuleConfiguration AddInterceptor(InterceptorType interceptorType, Func<IInvocationContext, Task> interceptorDelegate);

        /// <summary>
        /// Not implemented, will throw if used. Enables client-side endpoint overload usage. This needs the server to also enable endpoint overloading. Otherwise, it will fail.
        /// </summary>
        /// <returns>The current instance of <see cref="IServerModuleConfiguration"/> for method chaining.</returns>
        IServerModuleConfiguration EnableHttpEndpointOverloading();

        /// <summary>
        /// Specifies the http client factory to use. The factory must produce scoped or transient clients, otherwise contracts might overwrite other contract's http client settings.
        /// HttpClient is requested only once per singleton contract.
        /// </summary>
        /// <returns>The current instance of <see cref="IServerModuleConfiguration"/> for method chaining.</returns>
        IServerModuleConfiguration UseHttpClientFactory(Func<IServiceProvider, HttpClient> httpClientFactory);

        /// <summary>
        /// Specifies which transport should be used. By default, HTTP is used. Using attributes in contracts instead of this is recommended, unless you are exposing multiple transports.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns>The current instance of <see cref="IServerModuleConfiguration"/> for method chaining.</returns>
        IServerModuleConfiguration SetDefaultTransport<T>() where T : HubconTransportAttribute, new();

        /// <summary>
        /// Sets the default transport as WebSockets. Use this with Hubcon servers only. Using attributes in contracts instead of this is recommended.
        /// </summary>
        /// <returns>The current instance of <see cref="IServerModuleConfiguration"/> for method chaining.</returns>
        IServerModuleConfiguration UseWebSockets();

        /// <summary>
        /// Sets the default transport to HTTP. Use this with Hubcon servers only. Using attributes in contracts instead of this is recommended.
        /// </summary>
        /// <returns>The current instance of <see cref="IServerModuleConfiguration"/> for method chaining.</returns>
        IServerModuleConfiguration UseHttp();

        /// <summary>
        /// Sets the default transport to Non-Hubcon HTTP. Used for external integrations. Using attributes in contracts instead of this is recommended.
        /// </summary>
        /// <returns>The current instance of <see cref="IServerModuleConfiguration"/> for method chaining.</returns>
        IServerModuleConfiguration UseNonHubconHttp();

        /// <summary>
        /// Adds a header provider to this server module. Use the provided key by setting the attribute <see cref="HeaderAttribute"/> in contracts or endpoints.
        /// </summary>
        /// <param name="key">The key to match the header atributes.</param>
        /// <param name="valueProvider">A lambda function to provide the header value as a string.</param>
        /// <returns>The current instance of <see cref="IServerModuleConfiguration"/> for method chaining.</returns>
        IServerModuleConfiguration AddHeaderProvider(string key, Func<IServiceProvider, string> valueProvider);

        /// <summary>
        /// Configures whether the server module allows cancellation requests from remote clients.
        /// </summary>
        /// <remarks>Enabling remote cancellation allows server modules to respond to cancellation
        /// requests initiated by remote clients. This can be useful for managing long-running operations and improving
        /// responsiveness in distributed environments.</remarks>
        /// <param name="v">A value indicating whether remote cancellation is enabled. Specify <see langword="true"/> to permit remote
        /// clients to cancel operations; otherwise, <see langword="false"/>.</param>
        /// <returns>The current instance of <see cref="IServerModuleConfiguration"/> for method chaining.</returns>
        IServerModuleConfiguration AllowRemoteCancellation();

        /// <summary>
        /// Adds a new setting to the module's internal configurations, which is exposed to the used transports.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns>The current instance of <see cref="IServerModuleConfiguration"/> for method chaining.</returns>
        IServerModuleConfiguration AddSetting(string key, object? value);
    }
}