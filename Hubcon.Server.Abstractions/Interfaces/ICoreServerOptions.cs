using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Threading.RateLimiting;

namespace Hubcon.Server.Abstractions.Interfaces
{
    public interface ICoreServerOptions
    {
        /// <summary>
        /// Sets the maximum incoming message size for websockets. Default value is 16384 bytes.
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions SetMaxWebSocketMessageSize(int bytes);

        /// <summary>
        /// Sets the maximum incoming message size for HTTP.
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions SetMaxHttpMessageSize(int bytes);

        /// <summary>
        /// Set the timeout for websocket connections. If the connection remains silent for the specified time, the connection will automatically be closed.
        /// The client should send a ping message to keep the connection alive. Default is 30 seconds.
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions SetWebSocketTimeout(TimeSpan timeout);

        /// <summary>
        /// Set the timeout for HTTP connections. If a request takes longer than the specified time, the operation is cancelled. Default is 15 seconds.
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions SetHttpTimeout(TimeSpan timeout);

        /// <summary>
        /// Set the timeout for websocket ingest connections. If the connection remains silent for the specified time, the connection will automatically be closed.
        /// Use Timespan.Zero to disable the timeout. The default is 30 seconds.
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions SetWebSocketIngestTimeout(TimeSpan timeout);

        /// <summary>
        /// Determines if the server should send a pong message to the client.
        /// </summary>
        /// <param name="enabled"></param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions DisableWebSocketPong(bool enabled = true);

        /// <summary>
        /// Sets the path prefix that the websocket will be listening on.
        /// </summary>
        /// <param name="prefix"></param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions SetWebSocketPathPrefix(string prefix);

        /// <summary>
        /// Sets the path prefix that the HTTP endpoints will be bound to.
        /// </summary>
        /// <param name="prefix"></param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions SetHttpPathPrefix(string prefix);

        /// <summary>
        /// Disables or enables WebSocket ingest functionality for the server.
        /// </summary>
        /// <param name="disabled">A boolean value indicating whether WebSocket ingest should be disabled.  Pass <see langword="true"/> to
        /// disable WebSocket ingest; otherwise, <see langword="false"/>. The default value is <see langword="true"/>.</param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions DisableWebSocketIngest(bool disabled = true);

        /// <summary>
        /// Disables or enables WebSocket subscriptions for the server.
        /// </summary>
        /// <remarks>Use this method to control whether the server should allow WebSocket subscriptions. 
        /// This can be useful in scenarios where subscriptions are not required or should be
        /// restricted.</remarks>
        /// <param name="disabled">A boolean value indicating whether WebSocket subscriptions should be disabled.  Pass <see langword="true"/>
        /// to disable WebSocket subscriptions; otherwise, <see langword="false"/>.</param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions DisableWebSocketSubscriptions(bool disabled = true);

        /// <summary>
        /// Disables or enables WebSocket methods for the server.
        /// </summary>
        /// <param name="disabled">A boolean value indicating whether WebSocket methods should be disabled.  Pass <see langword="true"/> to
        /// disable WebSocket methods; otherwise, <see langword="false"/>.</param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions DisableWebSocketMethods(bool disabled = true);

        /// <summary>
        /// Disables or enables the WebSocket ping functionality.
        /// </summary>
        /// <param name="disabled">A value indicating whether WebSocket ping should be disabled.  Pass <see langword="true"/> to disable
        /// WebSocket ping; otherwise, <see langword="false"/>.</param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions DisableWebsocketPing(bool disabled = true);

        /// <summary>
        /// Configures whether retryable messages are disabled for the server.
        /// </summary>
        /// <param name="enabled">A boolean value indicating whether retryable messages should be disabled.  <see langword="true"/> to disable
        /// retryable messages; otherwise, <see langword="false"/>. The default value is <see langword="true"/>.</param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions DisabledRetryableMessages(bool enabled = true);

        /// <summary>
        /// Enables or disables detailed error messages for requests.
        /// </summary>
        /// <remarks>When detailed error messages are enabled, additional information about errors  may be
        /// included in responses, which can be useful for debugging purposes.  Use caution when enabling this in
        /// production environments, as it may expose  sensitive information.</remarks>
        /// <param name="enabled">A value indicating whether detailed error messages should be enabled.  The default is <see
        /// langword="true"/>.</param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions EnableRequestDetailedErrors(bool enabled = true);

        /// <summary>
        /// Disables or enables the WebSocket stream feature for the server.
        /// </summary>
        /// <param name="disabled">A boolean value indicating whether to disable the WebSocket stream.  Pass <see langword="true"/> to disable
        /// the WebSocket stream; otherwise, <see langword="false"/>. The default value is <see langword="true"/>.</param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions DisableWebSocketStream(bool disabled = true);

        /// <summary>
        /// Enables or disables the WebSocket logging feature.
        /// </summary>
        /// <param name="enabled"></param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions EnableWebsocketsLogging(bool enabled = true);

        /// <summary>
        /// Enables or disables the WebSocket logging feature.
        /// </summary>
        /// <param name="enabled"></param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions EnableHttpLogging(bool enabled = true);

        /// <summary>
        /// Defines how many operations can be processed concurrently for a single websocket client.
        /// </summary>
        /// <param name="enabled"></param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions SetMaxConcurrentOperations(int count);

        /// <summary>
        /// Sets a delay for websocket ingest message reception.
        /// </summary>
        /// <param name="delay"></param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions LimitWebsocketIngest(Func<TokenBucketRateLimiterOptions> rateLimiterOptionsFactory);

        /// <summary>
        /// Sets a delay for websocket methods message reception.
        /// </summary>
        /// <param name="delay"></param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions LimitWebsocketRoundTrip(Func<TokenBucketRateLimiterOptions> rateLimiterOptionsFactory);

        /// <summary>
        /// Sets a delay for sending websocket subscriptions messages.
        /// </summary>
        /// <param name="delay"></param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions LimitWebsocketSubscription(Func<TokenBucketRateLimiterOptions> rateLimiterOptionsFactory);

        /// <summary>
        /// Sets a delay for sending websocket streaming messages.
        /// </summary>
        /// <param name="delay"></param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions LimitWebsocketStreaming(Func<TokenBucketRateLimiterOptions> rateLimiterOptionsFactory);

        /// <summary>
        /// Disables all rate limiter options.
        /// </summary>
        /// <param name="delay"></param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions DisableAllRateLimiters();

        /// <summary>
        /// Configures a client-limited rate limiter for the server's websocket.
        /// </summary>
        /// <param name="rateLimiterOptionsFactory"></param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions ConfigureWebsocketRateLimiter(Func<TokenBucketRateLimiterOptions> rateLimiterOptionsFactory);

        /// <summary>
        /// Configures a client-limited ping rate limiter for the server's websocket.
        /// </summary>
        /// <param name="rateLimiterOptionsFactory"></param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions ConfigureWebsocketPingRateLimiter(Func<TokenBucketRateLimiterOptions> rateLimiterOptionsFactory);

        /// <summary>
        /// Configures a client-limited ping rate limiter for the server's websocket.
        /// </summary>
        /// <param name="rateLimiterOptionsFactory"></param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions ConfigureWebsocketTokenUpdateRateLimiter(Func<TokenBucketRateLimiterOptions> rateLimiterOptionsFactory);

        /// <summary>
        /// Configures a global route handler for the server using the specified builder action.
        /// </summary>
        /// <remarks>Use this method to apply routing conventions or middleware to all routes in the
        /// application. The configuration provided will affect every endpoint registered after this call.</remarks>
        /// <param name="configure">An action that receives a RouteHandlerBuilder to configure global routing behavior for all endpoints. Cannot
        /// be null.</param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions UseGlobalRouteHandlerBuilder(Action<RouteHandlerBuilder> configure);

        /// <summary>
        /// Configures global HTTP endpoint conventions for the application.
        /// </summary>
        /// <remarks>Use this method to apply consistent HTTP settings or behaviors across all endpoints,
        /// such as adding middleware, setting default headers, or enforcing policies.</remarks>
        /// <param name="configure">An action that defines the conventions to apply to all HTTP endpoints. Cannot be null.</param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions UseGlobalHttpConfigurations(Action<IEndpointConventionBuilder> configure);

        /// <summary>
        /// Configures HTTP round-trip rate limiting using the specified token bucket rate limiter options.
        /// </summary>
        /// <remarks>Use this method to prevent excessive HTTP request rates and protect server resources.
        /// Ensure that the provided options are tuned to match your application's expected traffic patterns and
        /// performance requirements.</remarks>
        /// <param name="rateLimiterOptionsFactory">A factory function that returns a configured instance of TokenBucketRateLimiterOptions to control the rate
        /// limiting behavior. Cannot be null.</param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions LimitHttpRoundTrip(Func<TokenBucketRateLimiterOptions> rateLimiterOptionsFactory);

        /// <summary>
        /// Configures the server options to allow remote cancellation of tokens.
        /// </summary>
        /// <remarks>Enabling remote token cancellation is useful in distributed or multi-service
        /// environments where cancellation tokens may need to be signaled across process or network boundaries. Use
        /// this method to permit such cross-boundary cancellation scenarios.</remarks>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions AllowRemoteTokenCancellation();

        /// <summary>
        /// Disables the expiration check for tokens in web service message processing.
        /// </summary>
        /// <remarks>Use this method when token expiration validation is not required for web service
        /// messages. Disabling this check may reduce security by allowing expired tokens to be accepted during message
        /// processing.</remarks>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions DisableTokenExpirationCheckOnWSMessage();

        /// <summary>
        /// Not implemented, will throw an exception on use. Enables support for endpoint overloading, allowing multiple identical endpoints with diferent parameters to be used.
        /// </summary>
        ICoreServerOptions EnableEndpointOverloading();

        /// <summary>
        /// Adds a transport of the specified type to the server options configuration.
        /// </summary>
        /// <remarks>Use this method to dynamically extend the server's supported transport mechanisms.
        /// Ensure that the transport type provided is properly configured and compatible with the server's
        /// architecture.</remarks>
        /// <typeparam name="T">The type of transport to add. Must inherit from HubconTransportAttribute and have a parameterless
        /// constructor.</typeparam>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions AddTransport<T>() where T : HubconTransportAttribute, new();

        /// <summary>
        /// Adds a transport attribute to the server options, enabling support for the specified transport mechanism.
        /// </summary>
        /// <remarks>Use this method to extend server communication capabilities by registering additional
        /// transport mechanisms. Ensure that the provided transport attribute is properly configured before adding
        /// it.</remarks>
        /// <typeparam name="T">The type of transport attribute to add. Must derive from HubconTransportAttribute.</typeparam>
        /// <param name="transportAttribute">The transport attribute that configures the transport mechanism to be added. Cannot be null.</param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions AddTransport<T>(T transportAttribute) where T : HubconTransportAttribute;

        /// <summary>
        /// Configures the global rate limiter for the server using the specified options.
        /// </summary>
        /// <remarks>Use this method to control how the server handles incoming requests under high load
        /// or to prevent abuse. Proper configuration can help maintain server responsiveness and reliability.</remarks>
        /// <param name="options">The rate limiter configuration options that define request limits and behavior. Cannot be null.</param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions SetGlobalRateLimiter(TokenBucketRateLimiterOptions options);

        /// <summary>
        /// Sets the global rate limiter for the server, specifying the maximum number of requests that can be processed
        /// per second.
        /// </summary>
        /// <remarks>Use this method to control server load and ensure fair resource allocation among
        /// clients. Adjusting the rate limiter can help prevent server overload during periods of high
        /// demand.</remarks>
        /// <param name="requestsPerSecond">The maximum number of requests allowed per second. Must be a positive integer.</param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions SetGlobalRateLimiter(int requests, int millisecondsToReplenish = 1000, int queueLimit = 0, int rateTokenLimit = 0);

        /// <summary>
        /// Configures the server to use transport authentication with the specified transport attribute and
        /// authentication handler types.
        /// </summary>
        /// <remarks>Use this method to integrate custom transport authentication logic by specifying both
        /// the transport attribute and the corresponding authentication handler. This enables flexible authentication
        /// strategies for different transport layers.</remarks>
        /// <typeparam name="TTransportAttribute">The transport attribute type that derives from <see cref="HubconTransportAttribute"/> and defines transport-specific
        /// authentication requirements.</typeparam>
        /// <typeparam name="TAuthHandler">The authentication handler type that implements <see cref="IUseAuthAttribute"/> and processes authentication for the
        /// specified transport.</typeparam>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions AddTransportAuth<TTransportAttribute, TAuthHandler>() 
            where TTransportAttribute : HubconTransportAttribute, new()
            where TAuthHandler : class, IAuthHandler;

        /// <summary>
        /// Allows anonymous WebSocket clients to connect to the server.
        /// </summary>
        /// <remarks>This method is useful for scenarios where unauthenticated clients need to establish
        /// WebSocket connections. Ensure that appropriate security measures are in place when allowing anonymous
        /// access.</remarks>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions AllowAnonymousWebSocketClients();
    }

    public interface IInternalServerOptions
    {
        /// <summary>
        /// Determines the maximum incoming websocket message size in bytes.
        /// </summary>
        public int MaxWebSocketMessageSize { get; }

        /// <summary>
        /// Disabled. Determines the maximum incoming http message size in bytes.
        /// </summary>
        public int MaxHttpMessageSize { get; }

        /// <summary>
        /// Websocket connection timeout when the
        /// </summary>
        public TimeSpan WebSocketTimeout { get; }

        /// <summary>
        /// Disabled. Http message processing timeout.
        /// </summary>
        public TimeSpan HttpTimeout { get; }

        /// <summary>
        /// Websocket ingest timeout.
        /// </summary>
        public TimeSpan IngestTimeout { get; }

        /// <summary>
        /// Determines if clients need to send ping messages to keep the connection alive.
        /// </summary>
        public bool WebsocketRequiresPing { get; }

        /// <summary>
        /// Determines if the websocket should handle RetryableMessage.
        /// </summary>
        public bool MessageRetryIsEnabled { get; }

        /// <summary>
        /// Determines if "pong" message is sent to the client when a ping message is received.
        /// </summary>
        public bool WebSocketPongEnabled { get; }

        /// <summary>
        /// Websocket prefix to bind to.
        /// </summary>
        public string WebSocketPathPrefix { get; }

        /// <summary>
        /// HTTP prefix to bind to.
        /// </summary>
        public string HttpPathPrefix { get; }

        /// <summary>
        /// Determines if ingest methods are allowed.
        /// </summary>
        public bool WebSocketIngestIsAllowed { get; }

        /// <summary>
        /// Determines if subscriptions are allowed.
        /// </summary>
        public bool WebSocketSubscriptionIsAllowed { get; }

        /// <summary>
        /// Determines if websocket streams are allowed.
        /// </summary>
        public bool WebSocketStreamIsAllowed { get; }

        /// <summary>
        /// Determines if typical controller methods are allowed.
        /// </summary>
        public bool WebSocketMethodsIsAllowed { get; }

        /// <summary>
        /// Determines if responses should include detailed error messages.
        /// </summary>
        public bool DetailedErrorsEnabled { get; }

        /// <summary>
        /// The websocket handler for authentication tokens.
        /// </summary>
        public bool WebsocketRequiresAuthorization { get; }

        /// <summary>
        /// Determines if the WebSocket ping feature is disabled.
        /// </summary>
        public bool WebsocketLoggingEnabled { get; }

        /// <summary>
        /// Determines if the HTTP logging feature is enabled.
        /// </summary>
        public bool HttpLoggingEnabled { get; }

        /// <summary>
        /// The websocket handler for authentication tokens.
        /// </summary>
        public Func<string, IServiceProvider, (ClaimsPrincipal, DateTime expirationDate)?>? TokenHandler { get; }

        /// <summary>
        /// Delay for a websocket client receive loop.
        /// </summary>
        public bool ThrottlingIsDisabled { get; }

        /// <summary>
        /// Allows configuring extra some global settings for HTTP endpoints.
        /// </summary>
        public Action<IEndpointConventionBuilder>? EndpointConventions { get; }

        /// <summary>
        /// Allows configuring extra some global settings for HTTP endpoints.
        /// </summary>
        public Action<RouteHandlerBuilder>? RouteHandlerBuilderConfig { get; }

        /// <summary>
        /// Allows configuring the rate limiter options for the server websocket.
        /// </summary>
        Func<TokenBucketRateLimiterOptions>? WebsocketReaderRateLimiter { get; }

        /// <summary>
        /// Allows configuring the ping rate limiter options for the server websocket.
        /// </summary>
        Func<TokenBucketRateLimiterOptions>? WebsocketPingRateLimiter { get; }

        /// <summary>
        /// Rate limiter options applied to round-trip (Task<T>) WebSocket methods.
        /// </summary>
        Func<TokenBucketRateLimiterOptions>? HttpRoundTripMethodRateLimiter { get; }

        /// <summary>
        /// Rate limiter options applied to fire-and-forget WebSocket methods (void or taks methods).
        /// </summary>
        Func<TokenBucketRateLimiterOptions>? HttpFireAndForgetMethodLimiter { get; }

        /// <summary>
        /// Rate limiter options applied to round-trip (Task<T>) WebSocket methods.
        /// </summary>
        Func<TokenBucketRateLimiterOptions>? WebsocketRoundTripMethodRateLimiter { get; }

        /// <summary>
        /// Rate limiter options applied to fire-and-forget WebSocket methods (void or taks methods).
        /// </summary>
        Func<TokenBucketRateLimiterOptions>? WebsocketFireAndForgetMethodLimiter { get; }

        /// <summary>
        /// Rate limiter options for ingest messages in the websocket channel.
        /// </summary>
        Func<TokenBucketRateLimiterOptions>? WebsocketIngestRateLimiter { get; }

        /// <summary>
        /// Rate limiter options for subscription messages in the websocket channel.
        /// </summary>
        Func<TokenBucketRateLimiterOptions>? WebsocketSubscriptionRateLimiter { get; }

        /// <summary>
        /// Default rate limiter options for streaming messages in the websocket channel.
        /// </summary>
        Func<TokenBucketRateLimiterOptions>? WebsocketStreamingRateLimiter { get; }

        /// <summary>
        /// Rate limiter options for websocket token updates.
        /// </summary>
        Func<TokenBucketRateLimiterOptions>? WebsocketTokenUpdateRateLimiter { get; }

        /// <summary>
        /// Defines if remote operation cancellation through cancellation token is allowed.
        /// </summary>
        bool RemoteCancellationIsAllowed { get; }

        /// <summary>
        /// Defines if token expiration should be checked when a websocket message is received.
        /// </summary>
        bool CheckTokenExpirationOnMsgReceived { get; }

        /// <summary>
        /// Defines if method overloading is enabled for endpoints.
        /// </summary>
        bool MethodOverloadingIsEnabled { get; }

        /// <summary>
        /// Defines how many operations can be processed at the same time for a single client.
        /// </summary>
        int MaxConcurrentOperations { get; }

        /// <summary>
        /// Gets a read-only dictionary that maps transport types to their associated HubconTransport attributes.
        /// </summary>
        IReadOnlyDictionary<Type, HubconTransportAttribute> DefaultTransports { get; }

        /// <summary>
        /// Global rate limiter options.
        /// </summary>
        TokenBucketRateLimiterOptions GlobalRateLimiterOptions { get; }

        /// <summary>
        /// Gets a read-only dictionary that maps transport attributes to their corresponding authentication handler
        /// types.
        /// </summary>
        /// <remarks>Use this property to retrieve the authentication handler type associated with a
        /// specific transport attribute. This enables dynamic selection of authentication logic based on the transport
        /// method in use. The returned dictionary is read-only and cannot be modified.</remarks>
        IReadOnlyDictionary<HubconTransportAttribute, Type> AuthHandlerTypes { get; }
    }
}