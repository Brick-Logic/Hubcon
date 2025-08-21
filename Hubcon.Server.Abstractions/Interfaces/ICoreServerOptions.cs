using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Threading.Channels;
using System.Threading.RateLimiting;

namespace Hubcon.Server.Core.Configuration
{
    public interface ICoreServerOptions
    {
        /// <summary>
        /// Sets the maximum incoming message size for websockets. Default value is 16384 bytes.
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        ICoreServerOptions SetMaxWebSocketMessageSize(int bytes);

        /// <summary>
        /// Sets the maximum incoming message size for HTTP.
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        ICoreServerOptions SetMaxHttpMessageSize(int bytes);

        /// <summary>
        /// Set the timeout for websocket connections. If the connection remains silent for the specified time, the connection will automatically be closed.
        /// The client should send a ping message to keep the connection alive. Default is 30 seconds.
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns></returns>
        ICoreServerOptions SetWebSocketTimeout(TimeSpan timeout);

        /// <summary>
        /// Set the timeout for HTTP connections. If a request takes longer than the specified time, the operation is cancelled. Default is 15 seconds.
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns></returns>
        ICoreServerOptions SetHttpTimeout(TimeSpan timeout);

        /// <summary>
        /// Set the timeout for websocket ingest connections. If the connection remains silent for the specified time, the connection will automatically be closed.
        /// Use Timespan.Zero to disable the timeout. The default is 30 seconds.
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns></returns>
        ICoreServerOptions SetWebSocketIngestTimeout(TimeSpan timeout);

        /// <summary>
        /// Determines if the server should send a pong message to the client.
        /// </summary>
        /// <param name="enabled"></param>
        /// <returns></returns>
        ICoreServerOptions DisableWebSocketPong(bool enabled = true);

        /// <summary>
        /// Sets the path prefix that the websocket will be listening on.
        /// </summary>
        /// <param name="prefix"></param>
        /// <returns></returns>
        ICoreServerOptions SetWebSocketPathPrefix(string prefix);

        /// <summary>
        /// Sets the path prefix that the HTTP endpoints will be bound to.
        /// </summary>
        /// <param name="prefix"></param>
        /// <returns></returns>
        ICoreServerOptions SetHttpPathPrefix(string prefix);

        /// <summary>
        /// Disables or enables WebSocket ingest functionality for the server.
        /// </summary>
        /// <param name="disabled">A boolean value indicating whether WebSocket ingest should be disabled.  Pass <see langword="true"/> to
        /// disable WebSocket ingest; otherwise, <see langword="false"/>. The default value is <see langword="true"/>.</param>
        /// <returns>An <see cref="ICoreServerOptions"/> instance representing the updated server configuration.</returns>
        ICoreServerOptions DisableWebSocketIngest(bool disabled = true);

        /// <summary>
        /// Disables or enables WebSocket subscriptions for the server.
        /// </summary>
        /// <remarks>Use this method to control whether the server should allow WebSocket subscriptions. 
        /// This can be useful in scenarios where subscriptions are not required or should be
        /// restricted.</remarks>
        /// <param name="disabled">A boolean value indicating whether WebSocket subscriptions should be disabled.  Pass <see langword="true"/>
        /// to disable WebSocket subscriptions; otherwise, <see langword="false"/>.</param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing for method chaining.</returns>
        ICoreServerOptions DisableWebSocketSubscriptions(bool disabled = true);

        /// <summary>
        /// Disables or enables WebSocket methods for the server.
        /// </summary>
        /// <param name="disabled">A boolean value indicating whether WebSocket methods should be disabled.  Pass <see langword="true"/> to
        /// disable WebSocket methods; otherwise, <see langword="false"/>.</param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing for method chaining.</returns>
        ICoreServerOptions DisableWebSocketMethods(bool disabled = true);

        /// <summary>
        /// Disables or enables the WebSocket ping functionality.
        /// </summary>
        /// <param name="disabled">A value indicating whether WebSocket ping should be disabled.  Pass <see langword="true"/> to disable
        /// WebSocket ping; otherwise, <see langword="false"/>.</param>
        /// <returns>The current instance of <see cref="ICoreServerOptions"/>, allowing for method chaining.</returns>
        ICoreServerOptions DisableWebsocketPing(bool disabled = true);

        /// <summary>
        /// Configures whether retryable messages are disabled for the server.
        /// </summary>
        /// <param name="enabled">A boolean value indicating whether retryable messages should be disabled.  <see langword="true"/> to disable
        /// retryable messages; otherwise, <see langword="false"/>. The default value is <see langword="true"/>.</param>
        /// <returns>An instance of <see cref="ICoreServerOptions"/> to allow for method chaining.</returns>
        ICoreServerOptions DisabledRetryableMessages(bool enabled = true);

        /// <summary>
        /// Enables or disables detailed error messages for requests.
        /// </summary>
        /// <remarks>When detailed error messages are enabled, additional information about errors  may be
        /// included in responses, which can be useful for debugging purposes.  Use caution when enabling this in
        /// production environments, as it may expose  sensitive information.</remarks>
        /// <param name="enabled">A value indicating whether detailed error messages should be enabled.  The default is <see
        /// langword="true"/>.</param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing for method chaining.</returns>
        ICoreServerOptions EnableRequestDetailedErrors(bool enabled = true);

        /// <summary>
        /// Disables or enables the WebSocket stream feature for the server.
        /// </summary>
        /// <param name="disabled">A boolean value indicating whether to disable the WebSocket stream.  Pass <see langword="true"/> to disable
        /// the WebSocket stream; otherwise, <see langword="false"/>. The default value is <see langword="true"/>.</param>
        /// <returns>An instance of <see cref="ICoreServerOptions"/> to allow method chaining for further configuration.</returns>
        ICoreServerOptions DisableWebSocketStream(bool disabled = true);

        /// <summary>
        /// Enables or disables the WebSocket logging feature.
        /// </summary>
        /// <param name="enabled"></param>
        /// <returns></returns>
        ICoreServerOptions EnableWebsocketsLogging(bool enabled = true);

        /// <summary>
        /// Enables or disables the WebSocket logging feature.
        /// </summary>
        /// <param name="enabled"></param>
        /// <returns></returns>
        ICoreServerOptions EnableHttpLogging(bool enabled = true);

        /// <summary>
        /// Configures a handler to process WebSocket token authentication.
        /// </summary>
        /// <remarks>The provided <paramref name="tokenHandler"/> is invoked to validate and extract user
        /// information  from a WebSocket token. Ensure the handler is thread-safe if used in a multi-threaded
        /// environment.</remarks>
        /// <param name="tokenHandler">A function that returns a <see cref="ClaimsPrincipal"/> representing the authenticated user,  or <see
        /// langword="null"/> if authentication fails.</param>
        /// <returns>The <see cref="ICoreServerOptions"/> instance, allowing for method chaining.</returns>
        ICoreServerOptions UseWebsocketTokenHandler(Func<string, IServiceProvider, ClaimsPrincipal?> tokenHandler);

        /// <summary>
        /// Sets a delay for websocket ingest message reception.
        /// </summary>
        /// <param name="delay"></param>
        /// <returns></returns>
        ICoreServerOptions LimitWebsocketIngest(Func<TokenBucketRateLimiterOptions> rateLimiterOptionsFactory);

        /// <summary>
        /// Sets a delay for websocket methods message reception.
        /// </summary>
        /// <param name="delay"></param>
        /// <returns></returns>
        ICoreServerOptions LimitWebsocketRoundTrip(Func<TokenBucketRateLimiterOptions> rateLimiterOptionsFactory);

        /// <summary>
        /// Sets a delay for sending websocket subscriptions messages.
        /// </summary>
        /// <param name="delay"></param>
        /// <returns></returns>
        ICoreServerOptions LimitWebsocketSubscription(Func<TokenBucketRateLimiterOptions> rateLimiterOptionsFactory);

        /// <summary>
        /// Sets a delay for sending websocket streaming messages.
        /// </summary>
        /// <param name="delay"></param>
        /// <returns></returns>
        ICoreServerOptions LimitWebsocketStreaming(Func<TokenBucketRateLimiterOptions> rateLimiterOptionsFactory);

        /// <summary>
        /// Disables all rate limiter options.
        /// </summary>
        /// <param name="delay"></param>
        /// <returns></returns>
        ICoreServerOptions DisableAllRateLimiters();

        /// <summary>
        /// Configures a client-limited rate limiter for the server's websocket.
        /// </summary>
        /// <param name="rateLimiterOptionsFactory"></param>
        /// <returns></returns>
        ICoreServerOptions ConfigureWebsocketRateLimiter(Func<TokenBucketRateLimiterOptions> rateLimiterOptionsFactory);

        /// <summary>
        /// Configures a client-limited ping rate limiter for the server's websocket.
        /// </summary>
        /// <param name="rateLimiterOptionsFactory"></param>
        /// <returns></returns>
        ICoreServerOptions ConfigureWebsocketPingRateLimiter(Func<TokenBucketRateLimiterOptions> rateLimiterOptionsFactory);

        ICoreServerOptions UseGlobalRouteHandlerBuilder(Action<RouteHandlerBuilder> configure);
        ICoreServerOptions UseGlobalHttpConfigurations(Action<IEndpointConventionBuilder> configure);
        ICoreServerOptions LimitHttpRoundTrip(Func<TokenBucketRateLimiterOptions> rateLimiterOptionsFactory);
        ICoreServerOptions AllowRemoteTokenCancellation();
    }

    public interface IInternalServerOptions
    {
        /// <summary>
        /// Determines the maximum incoming websocket message size in bytes.
        /// </summary>
        int MaxWebSocketMessageSize { get; }

        /// <summary>
        /// Disabled. Determines the maximum incoming http message size in bytes.
        /// </summary>
        int MaxHttpMessageSize { get; }

        /// <summary>
        /// Websocket connection timeout when the
        /// </summary>
        TimeSpan WebSocketTimeout { get; }

        /// <summary>
        /// Disabled. Http message processing timeout.
        /// </summary>
        TimeSpan HttpTimeout { get; }

        /// <summary>
        /// Websocket ingest timeout.
        /// </summary>
        TimeSpan IngestTimeout { get; }

        /// <summary>
        /// Determines if clients need to send ping messages to keep the connection alive.
        /// </summary>
        bool WebsocketRequiresPing { get; }

        /// <summary>
        /// Determines if the websocket should handle RetryableMessage.
        /// </summary>
        bool MessageRetryIsEnabled { get; }

        /// <summary>
        /// Determines if "pong" message is sent to the client when a ping message is received.
        /// </summary>
        bool WebSocketPongEnabled { get; }

        /// <summary>
        /// Websocket prefix to bind to.
        /// </summary>
        string WebSocketPathPrefix { get; }

        /// <summary>
        /// HTTP prefix to bind to.
        /// </summary>
        string HttpPathPrefix { get; }

        /// <summary>
        /// Determines if ingest methods are allowed.
        /// </summary>
        bool WebSocketIngestIsAllowed { get; }

        /// <summary>
        /// Determines if subscriptions are allowed.
        /// </summary>
        bool WebSocketSubscriptionIsAllowed { get; }

        /// <summary>
        /// Determines if websocket streams are allowed.
        /// </summary>
        bool WebSocketStreamIsAllowed { get; }

        /// <summary>
        /// Determines if typical controller methods are allowed.
        /// </summary>
        bool WebSocketMethodsIsAllowed { get; }

        /// <summary>
        /// Determines if responses should include detailed error messages.
        /// </summary>
        bool DetailedErrorsEnabled { get; }

        /// <summary>
        /// The websocket handler for authentication tokens.
        /// </summary>
        bool WebsocketRequiresAuthorization { get; }

        /// <summary>
        /// Determines if the WebSocket ping feature is disabled.
        /// </summary>
        bool WebsocketLoggingEnabled { get; }

        /// <summary>
        /// Determines if the HTTP logging feature is enabled.
        /// </summary>
        bool HttpLoggingEnabled { get; }

        /// <summary>
        /// The websocket handler for authentication tokens.
        /// </summary>
        Func<string, IServiceProvider, ClaimsPrincipal?>? WebsocketTokenHandler { get; }

        /// <summary>
        /// Delay for a websocket client receive loop.
        /// </summary>
        bool ThrottlingIsDisabled { get; }

        /// <summary>
        /// Allows configuring extra some global settings for HTTP endpoints.
        /// </summary>
        Action<IEndpointConventionBuilder>? EndpointConventions { get; }

        /// <summary>
        /// Allows configuring extra some global settings for HTTP endpoints.
        /// </summary>
        Action<RouteHandlerBuilder> RouteHandlerBuilderConfig { get; }

        /// <summary>
        /// Allows configuring the rate limiter options for the server websocket.
        /// </summary>
        Func<TokenBucketRateLimiterOptions> WebsocketReaderRateLimiter { get; }

        /// <summary>
        /// Allows configuring the ping rate limiter options for the server websocket.
        /// </summary>
        Func<TokenBucketRateLimiterOptions> WebsocketPingRateLimiter { get; }

        /// <summary>
        /// Rate limiter options applied to round-trip (Task<T>) WebSocket methods.
        /// </summary>
        Func<TokenBucketRateLimiterOptions> HttpRoundTripMethodRateLimiter { get; }

        /// <summary>
        /// Rate limiter options applied to fire-and-forget WebSocket methods (void or taks methods).
        /// </summary>
        Func<TokenBucketRateLimiterOptions> HttpFireAndForgetMethodLimiter { get; }

        /// <summary>
        /// Rate limiter options applied to round-trip (Task<T>) WebSocket methods.
        /// </summary>
        Func<TokenBucketRateLimiterOptions> WebsocketRoundTripMethodRateLimiter { get; }

        /// <summary>
        /// Rate limiter options applied to fire-and-forget WebSocket methods (void or taks methods).
        /// </summary>
        Func<TokenBucketRateLimiterOptions> WebsocketFireAndForgetMethodLimiter { get; }

        /// <summary>
        /// Rate limiter options for ingest messages in the websocket channel.
        /// </summary>
        Func<TokenBucketRateLimiterOptions> WebsocketIngestRateLimiter { get; }

        /// <summary>
        /// Rate limiter options for subscription messages in the websocket channel.
        /// </summary>
        Func<TokenBucketRateLimiterOptions> WebsocketSubscriptionRateLimiter { get; }

        /// <summary>
        /// Default rate limiter options for streaming messages in the websocket channel.
        /// </summary>
        Func<TokenBucketRateLimiterOptions> WebsocketStreamingRateLimiter { get; }
        
        /// <summary>
        /// Determines if remote task cancellation is allowed.
        /// </summary>
        bool RemoteCancellationIsAllowed { get; }
    }
}