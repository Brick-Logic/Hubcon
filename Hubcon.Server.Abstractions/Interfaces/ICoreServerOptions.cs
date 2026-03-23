using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Threading.RateLimiting;

namespace Hubcon.Server.Abstractions.Interfaces
{
    /// <summary>
    /// Defines the configuration interface for the Hubcon server core.
    /// Provides methods to tune protocol behavior, timeouts, limits, and security settings.
    /// </summary>
    public interface ICoreServerOptions
    {
        /// <summary>
        /// Sets the maximum incoming message size for WebSockets.
        /// </summary>
        /// <param name="bytes">The maximum size in bytes. The default value is 16384 bytes.</param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions SetMaxWebSocketMessageSize(int bytes);

        /// <summary>
        /// Sets the maximum incoming message size for HTTP requests.
        /// </summary>
        /// <param name="bytes">The maximum size in bytes.</param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions SetMaxHttpMessageSize(int bytes);

        /// <summary>
        /// Sets the timeout for WebSocket connections. If the connection remains silent for the specified time, 
        /// it will be automatically closed.
        /// </summary>
        /// <remarks>
        /// The client should send a ping message to keep the connection alive. The default is 30 seconds.
        /// </remarks>
        /// <param name="timeout">The duration of the timeout.</param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions SetWebSocketTimeout(TimeSpan timeout);

        /// <summary>
        /// Sets the timeout for HTTP connections. If a request takes longer than the specified time, the operation is cancelled.
        /// </summary>
        /// <param name="timeout">The duration of the timeout. The default is 15 seconds.</param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions SetHttpTimeout(TimeSpan timeout);

        /// <summary>
        /// Sets the timeout for WebSocket ingest connections. If the connection remains silent for the specified time, 
        /// it will be automatically closed.
        /// </summary>
        /// <param name="timeout">The duration of the timeout. Use <see cref="TimeSpan.Zero"/> to disable. The default is 30 seconds.</param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions SetWebSocketIngestTimeout(TimeSpan timeout);

        /// <summary>
        /// Configures whether the server should automatically send a "pong" response when a "ping" is received from a client.
        /// </summary>
        /// <param name="enabled"><see langword="true"/> to disable the pong response; otherwise, <see langword="false"/>.</param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions DisableWebSocketPong(bool enabled = true);

        /// <summary>
        /// Sets the path prefix that the WebSocket will listen on.
        /// </summary>
        /// <param name="prefix">The URL path prefix.</param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions SetWebSocketPathPrefix(string prefix);

        /// <summary>
        /// Sets the path prefix that the HTTP endpoints will be bound to.
        /// </summary>
        /// <param name="prefix">The URL path prefix.</param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions SetHttpPathPrefix(string prefix);

        /// <summary>
        /// Disables or enables WebSocket ingest functionality for the server.
        /// </summary>
        /// <param name="disabled"><see langword="true"/> to disable WebSocket ingest; otherwise, <see langword="false"/>. The default value is <see langword="true"/>.</param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions DisableWebSocketIngest(bool disabled = true);

        /// <summary>
        /// Disables or enables WebSocket subscriptions for the server.
        /// </summary>
        /// <param name="disabled"><see langword="true"/> to disable WebSocket subscriptions; otherwise, <see langword="false"/>. The default value is <see langword="true"/>.</param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions DisableWebSocketSubscriptions(bool disabled = true);

        /// <summary>
        /// Disables or enables standard WebSocket controller methods for the server.
        /// </summary>
        /// <param name="disabled"><see langword="true"/> to disable methods; otherwise, <see langword="false"/>.</param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions DisableWebSocketMethods(bool disabled = true);

        /// <summary>
        /// Disables or enables the WebSocket ping requirement.
        /// </summary>
        /// <param name="disabled"><see langword="true"/> to disable pings; otherwise, <see langword="false"/>.</param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions DisableWebsocketPing(bool disabled = true);

        /// <summary>
        /// Configures whether retryable messages are disabled for the server.
        /// </summary>
        /// <param name="enabled"><see langword="true"/> to disable retryable messages; otherwise, <see langword="false"/>.</param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions DisabledRetryableMessages(bool enabled = true);

        /// <summary>
        /// Enables or disables detailed error messages in the operation responses.
        /// </summary>
        /// <remarks>
        /// When enabled, additional diagnostic information may be included in error responses. 
        /// Use caution in production as it may expose internal system details.
        /// </remarks>
        /// <param name="enabled"><see langword="true"/> to enable detailed errors; otherwise, <see langword="false"/>.</param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions EnableRequestDetailedErrors(bool enabled = true);

        /// <summary>
        /// Disables or enables the WebSocket stream feature for the server.
        /// </summary>
        /// <param name="disabled"><see langword="true"/> to disable streaming; otherwise, <see langword="false"/>.</param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions DisableWebSocketStream(bool disabled = true);

        /// <summary>
        /// Enables or disables internal logging for WebSocket activities.
        /// </summary>
        /// <param name="enabled"><see langword="true"/> to enable logging; otherwise, <see langword="false"/>.</param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions EnableWebsocketsLogging(bool enabled = true);

        /// <summary>
        /// Enables or disables internal logging for HTTP activities.
        /// </summary>
        /// <param name="enabled"><see langword="true"/> to enable logging; otherwise, <see langword="false"/>.</param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions EnableHttpLogging(bool enabled = true);

        /// <summary>
        /// Defines the maximum number of operations that can be processed concurrently for a single WebSocket client.
        /// </summary>
        /// <param name="count">The maximum number of concurrent operations.</param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions SetMaxConcurrentOperations(int count);

        /// <summary>
        /// Configures the rate limiter for WebSocket ingest messages.
        /// </summary>
        /// <param name="rateLimiterOptionsFactory">A factory function returning the <see cref="TokenBucketRateLimiterOptions"/>.</param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions LimitWebsocketIngest(Func<TokenBucketRateLimiterOptions> rateLimiterOptionsFactory);

        /// <summary>
        /// Configures the rate limiter for WebSocket round-trip (request/response) messages.
        /// </summary>
        /// <param name="rateLimiterOptionsFactory">A factory function returning the <see cref="TokenBucketRateLimiterOptions"/>.</param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions LimitWebsocketRoundTrip(Func<TokenBucketRateLimiterOptions> rateLimiterOptionsFactory);

        /// <summary>
        /// Configures the rate limiter for sending WebSocket subscription updates.
        /// </summary>
        /// <param name="rateLimiterOptionsFactory">A factory function returning the <see cref="TokenBucketRateLimiterOptions"/>.</param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions LimitWebsocketSubscription(Func<TokenBucketRateLimiterOptions> rateLimiterOptionsFactory);

        /// <summary>
        /// Configures the rate limiter for WebSocket streaming messages.
        /// </summary>
        /// <param name="rateLimiterOptionsFactory">A factory function returning the <see cref="TokenBucketRateLimiterOptions"/>.</param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions LimitWebsocketStreaming(Func<TokenBucketRateLimiterOptions> rateLimiterOptionsFactory);

        /// <summary>
        /// Disables all configured rate limiters on the server.
        /// </summary>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions DisableAllRateLimiters();

        /// <summary>
        /// Configures a client-specific rate limiter for the general WebSocket reader.
        /// </summary>
        /// <param name="rateLimiterOptionsFactory">A factory function returning the <see cref="TokenBucketRateLimiterOptions"/>.</param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions ConfigureWebsocketRateLimiter(Func<TokenBucketRateLimiterOptions> rateLimiterOptionsFactory);

        /// <summary>
        /// Configures a client-specific rate limiter for WebSocket ping messages.
        /// </summary>
        /// <param name="rateLimiterOptionsFactory">A factory function returning the <see cref="TokenBucketRateLimiterOptions"/>.</param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions ConfigureWebsocketPingRateLimiter(Func<TokenBucketRateLimiterOptions> rateLimiterOptionsFactory);

        /// <summary>
        /// Configures a client-specific rate limiter for WebSocket token update requests.
        /// </summary>
        /// <param name="rateLimiterOptionsFactory">A factory function returning the <see cref="TokenBucketRateLimiterOptions"/>.</param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions ConfigureWebsocketTokenUpdateRateLimiter(Func<TokenBucketRateLimiterOptions> rateLimiterOptionsFactory);

        /// <summary>
        /// Configures a global route handler builder to be applied to all registered routes.
        /// </summary>
        /// <param name="configure">An action to configure the <see cref="RouteHandlerBuilder"/>.</param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions UseGlobalRouteHandlerBuilder(Action<RouteHandlerBuilder> configure);

        /// <summary>
        /// Configures global settings and conventions for all HTTP endpoints.
        /// </summary>
        /// <param name="configure">An action to configure the <see cref="IEndpointConventionBuilder"/>.</param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions UseGlobalHttpConfigurations(Action<IEndpointConventionBuilder> configure);

        /// <summary>
        /// Configures rate limiting for HTTP round-trip requests.
        /// </summary>
        /// <param name="rateLimiterOptionsFactory">A factory function returning the <see cref="TokenBucketRateLimiterOptions"/>.</param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions LimitHttpRoundTrip(Func<TokenBucketRateLimiterOptions> rateLimiterOptionsFactory);

        /// <summary>
        /// Enables the ability for clients to remotely signal the cancellation of server-side tokens.
        /// </summary>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions AllowRemoteTokenCancellation();

        /// <summary>
        /// Disables the expiration check for tokens when a WebSocket message is received.
        /// </summary>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions DisableTokenExpirationCheckOnWSMessage();

        /// <summary>
        /// <b>Not Implemented:</b> Enables support for endpoint overloading, allowing multiple endpoints 
        /// with the same name but different parameters. Throws an exception if used.
        /// </summary>
        ICoreServerOptions EnableEndpointOverloading();

        /// <summary>
        /// Adds a transport mechanism of the specified type.
        /// </summary>
        /// <typeparam name="T">A type deriving from <see cref="HubconTransportAttribute"/> with a parameterless constructor.</typeparam>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions AddTransport<T>() where T : HubconTransportAttribute, new();

        /// <summary>
        /// Adds a transport mechanism using a specific attribute instance.
        /// </summary>
        /// <typeparam name="T">A type deriving from <see cref="HubconTransportAttribute"/>.</typeparam>
        /// <param name="transportAttribute">The transport configuration attribute.</param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions AddTransport<T>(T transportAttribute) where T : HubconTransportAttribute;

        /// <summary>
        /// Sets a global token bucket rate limiter for the entire server.
        /// </summary>
        /// <param name="options">The <see cref="TokenBucketRateLimiterOptions"/> configuration.</param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions SetGlobalRateLimiter(TokenBucketRateLimiterOptions options);

        /// <summary>
        /// Configures a global token bucket rate limiter for the server with the specified parameters.
        /// </summary>
        /// <param name="requests">The number of tokens added per replenishment period.</param>
        /// <param name="millisecondsToReplenish">The interval in milliseconds between replenishment periods. Defaults to 1000ms.</param>
        /// <param name="queueLimit">Maximum number of requests that can be queued. Defaults to 0.</param>
        /// <param name="rateTokenLimit">Maximum total tokens allowed in the bucket. Defaults to 0.</param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions SetGlobalRateLimiter(int requests, int millisecondsToReplenish = 1000, int queueLimit = 0, int rateTokenLimit = 0);

        /// <summary>
        /// Registers a specialized authentication handler for a specific transport type.
        /// </summary>
        /// <typeparam name="TTransportAttribute">The transport attribute type.</typeparam>
        /// <typeparam name="TAuthHandler">The handler type implementing <see cref="IAuthHandler"/>.</typeparam>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions AddTransportAuth<TTransportAttribute, TAuthHandler>()
            where TTransportAttribute : HubconTransportAttribute, new()
            where TAuthHandler : class, IAuthHandler;

        /// <summary>
        /// Configures the server to allow connections from unauthenticated WebSocket clients.
        /// </summary>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions AllowAnonymousWebSocketClients();

        /// <summary>
        /// Configures the <see cref="TokenValidationParameters"/> used by the server's authentication 
        /// and authorization mechanisms to validate incoming security tokens.
        /// </summary>
        /// <param name="tokenValidationParameters">The <see cref="TokenValidationParameters"/> containing the rules for signature, issuer, and lifetime validation.</param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance for fluent configuration.</returns>
        ICoreServerOptions SetTokenValidationParameters(TokenValidationParameters tokenValidationParameters);
    }

    /// <summary>
    /// Defines the internal configuration and runtime constraints for the Hubcon server.
    /// This interface provides read-only access to protocol-specific limits, timeouts, and feature flags.
    /// </summary>
    public interface IInternalServerOptions
    {
        /// <summary>
        /// Gets the maximum allowed size, in bytes, for an incoming WebSocket message.
        /// </summary>
        public int MaxWebSocketMessageSize { get; }

        /// <summary>
        /// Gets the maximum allowed size, in bytes, for an incoming HTTP message.
        /// <remarks>This property is currently disabled.</remarks>
        /// </summary>
        public int MaxHttpMessageSize { get; }

        /// <summary>
        /// Gets the timeout duration for a WebSocket connection.
        /// </summary>
        public TimeSpan WebSocketTimeout { get; }

        /// <summary>
        /// Gets the timeout duration for processing an HTTP message.
        /// <remarks>This property is currently disabled.</remarks>
        /// </summary>
        public TimeSpan HttpTimeout { get; }

        /// <summary>
        /// Gets the timeout duration for WebSocket ingest operations.
        /// </summary>
        public TimeSpan IngestTimeout { get; }

        /// <summary>
        /// Gets a value indicating whether clients are required to send periodic ping messages to maintain the connection.
        /// </summary>
        public bool WebsocketRequiresPing { get; }

        /// <summary>
        /// Gets a value indicating whether the WebSocket handler should process <c>RetryableMessage</c> types.
        /// </summary>
        public bool MessageRetryIsEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether the server automatically sends a "pong" response upon receiving a "ping" from a client.
        /// </summary>
        public bool WebSocketPongEnabled { get; }

        /// <summary>
        /// Gets the URL path prefix used for WebSocket endpoint binding.
        /// </summary>
        public string WebSocketPathPrefix { get; }

        /// <summary>
        /// Gets the URL path prefix used for HTTP endpoint binding.
        /// </summary>
        public string HttpPathPrefix { get; }

        /// <summary>
        /// Gets a value indicating whether ingest-style methods are permitted over WebSocket connections.
        /// </summary>
        public bool WebSocketIngestIsAllowed { get; }

        /// <summary>
        /// Gets a value indicating whether subscription-based patterns are permitted over WebSocket connections.
        /// </summary>
        public bool WebSocketSubscriptionIsAllowed { get; }

        /// <summary>
        /// Gets a value indicating whether streaming operations are permitted over WebSocket connections.
        /// </summary>
        public bool WebSocketStreamIsAllowed { get; }

        /// <summary>
        /// Gets a value indicating whether standard controller methods are permitted over WebSocket connections.
        /// </summary>
        public bool WebSocketMethodsIsAllowed { get; }

        /// <summary>
        /// Gets a value indicating whether responses should include detailed exception information and stack traces.
        /// </summary>
        public bool DetailedErrorsEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether WebSocket connections require a valid authorization token.
        /// </summary>
        public bool WebsocketRequiresAuthorization { get; }

        /// <summary>
        /// Gets a value indicating whether logging is enabled for WebSocket-specific events.
        /// </summary>
        public bool WebsocketLoggingEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether logging is enabled for HTTP-specific events.
        /// </summary>
        public bool HttpLoggingEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether request throttling is disabled for the server.
        /// </summary>
        public bool ThrottlingIsDisabled { get; }

        /// <summary>
        /// Gets a delegate used to configure additional global metadata or conventions for HTTP endpoints.
        /// </summary>
        public Action<IEndpointConventionBuilder>? EndpointConventions { get; }

        /// <summary>
        /// Gets a delegate used to configure the <see cref="RouteHandlerBuilder"/> for HTTP-based routes.
        /// </summary>
        public Action<RouteHandlerBuilder>? RouteHandlerBuilderConfig { get; }

        /// <summary>
        /// Gets the factory for the rate limiter applied to general WebSocket reading operations.
        /// </summary>
        Func<TokenBucketRateLimiterOptions>? WebsocketReaderRateLimiter { get; }

        /// <summary>
        /// Gets the factory for the rate limiter applied specifically to WebSocket ping messages.
        /// </summary>
        Func<TokenBucketRateLimiterOptions>? WebsocketPingRateLimiter { get; }

        /// <summary>
        /// Gets the factory for the rate limiter applied to synchronous or Task-returning HTTP methods (Round-Trip).
        /// </summary>
        Func<TokenBucketRateLimiterOptions>? HttpRoundTripMethodRateLimiter { get; }

        /// <summary>
        /// Gets the factory for the rate limiter applied to asynchronous or void-returning HTTP methods (Fire-and-Forget).
        /// </summary>
        Func<TokenBucketRateLimiterOptions>? HttpFireAndForgetMethodLimiter { get; }

        /// <summary>
        /// Gets the factory for the rate limiter applied to synchronous or Task-returning WebSocket methods (Round-Trip).
        /// </summary>
        Func<TokenBucketRateLimiterOptions>? WebsocketRoundTripMethodRateLimiter { get; }

        /// <summary>
        /// Gets the factory for the rate limiter applied to asynchronous or void-returning WebSocket methods (Fire-and-Forget).
        /// </summary>
        Func<TokenBucketRateLimiterOptions>? WebsocketFireAndForgetMethodLimiter { get; }

        /// <summary>
        /// Gets the factory for the rate limiter applied to ingest-style messages over WebSocket.
        /// </summary>
        Func<TokenBucketRateLimiterOptions>? WebsocketIngestRateLimiter { get; }

        /// <summary>
        /// Gets the factory for the rate limiter applied to subscription-related messages over WebSocket.
        /// </summary>
        Func<TokenBucketRateLimiterOptions>? WebsocketSubscriptionRateLimiter { get; }

        /// <summary>
        /// Gets the factory for the rate limiter applied to streaming data over WebSocket.
        /// </summary>
        Func<TokenBucketRateLimiterOptions>? WebsocketStreamingRateLimiter { get; }

        /// <summary>
        /// Gets the factory for the rate limiter applied to WebSocket token update requests.
        /// </summary>
        Func<TokenBucketRateLimiterOptions>? WebsocketTokenUpdateRateLimiter { get; }

        /// <summary>
        /// Gets a value indicating whether the server supports remote operation cancellation via <see cref="CancellationToken"/>.
        /// </summary>
        bool RemoteCancellationIsAllowed { get; }

        /// <summary>
        /// Gets a value indicating whether the server should validate token expiration every time a new WebSocket message is received.
        /// </summary>
        bool CheckTokenExpirationOnMsgReceived { get; }

        /// <summary>
        /// Gets a value indicating whether method overloading is supported for server endpoints.
        /// </summary>
        bool MethodOverloadingIsEnabled { get; }

        /// <summary>
        /// Gets the maximum number of concurrent operations allowed for a single client connection.
        /// </summary>
        int MaxConcurrentOperations { get; }

        /// <summary>
        /// Gets a read-only dictionary mapping transport types to their associated <see cref="HubconTransportAttribute"/>.
        /// </summary>
        IReadOnlyDictionary<Type, HubconTransportAttribute> DefaultTransports { get; }

        /// <summary>
        /// Gets the global rate limiting configuration applied to the entire server instance.
        /// </summary>
        TokenBucketRateLimiterOptions GlobalRateLimiterOptions { get; }

        /// <summary>
        /// Gets a read-only dictionary that maps transport attributes to their corresponding authentication handler types.
        /// </summary>
        /// <remarks>
        /// Use this property to retrieve the authentication handler type associated with a specific transport attribute, 
        /// enabling dynamic selection of authentication logic based on the transport method in use.
        /// </remarks>
        IReadOnlyDictionary<HubconTransportAttribute, Type> AuthHandlerTypes { get; }

        /// <summary>
        /// Registered validation token parameters for authentication.
        /// </summary>
        TokenValidationParameters? TokenValidationParameters { get; }
    }
}