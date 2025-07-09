using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

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
        /// Determines if websocket ingest methods are allowed.
        /// </summary>
        /// <param name="enabled"></param>
        /// <returns></returns>
        ICoreServerOptions DisableWebSocketIngest(bool disabled = true);

        /// <summary>
        /// Determines if websocket subscriptions methods and ISubscription event handlers are allowed.
        /// </summary>
        /// <param name="enabled"></param>
        /// <returns></returns>
        ICoreServerOptions DisableWebSocketSubscriptions(bool disabled = true);

        /// <summary>
        /// Determines if typical controller methods are allowed through the websocket connection. This enables websocket-based controllers.
        /// </summary>
        /// <param name="enabled"></param>
        /// <returns></returns>
        ICoreServerOptions DisableWebSocketMethods(bool disabled = true);

        /// <summary>
        /// Determines if receiving ping messages from clients is required to keep the websocket connection alive.
        /// </summary>
        /// <param name="enabled"></param>
        /// <returns></returns>
        ICoreServerOptions DisableWebsocketPing(bool disabled = true);

        /// <summary>
        /// Determines if retryable messages should be used when detected.
        /// </summary>
        /// <param name="enabled"></param>
        /// <returns></returns>
        ICoreServerOptions DisabledRetryableMessages(bool enabled = true);

        /// <summary>
        /// Tells the global exception middleware to include detailed error messages in error responses.
        /// </summary>
        /// <param name="enabled"></param>
        /// <returns></returns>
        ICoreServerOptions EnableRequestDetailedErrors(bool enabled = true);

        /// <summary>
        /// Disables server to client streaming via websockets.
        /// </summary>
        /// <param name="disabled"></param>
        /// <returns></returns>
        ICoreServerOptions DisableWebSocketStream(bool disabled = true);

        /// <summary>
        /// Sets a delay for websocket ingest message reception.
        /// </summary>
        /// <param name="delay"></param>
        /// <returns></returns>
        ICoreServerOptions ThrottleWebsocketIngest(TimeSpan delay);

        /// <summary>
        /// Sets a delay for websocket methods message reception.
        /// </summary>
        /// <param name="delay"></param>
        /// <returns></returns>
        ICoreServerOptions ThrottleWebsocketMethods(TimeSpan delay);

        /// <summary>
        /// Sets a delay for sending websocket subscriptions messages.
        /// </summary>
        /// <param name="delay"></param>
        /// <returns></returns>
        ICoreServerOptions ThrottleWebsocketSubscription(TimeSpan delay);

        /// <summary>
        /// Sets a delay for sending websocket streaming messages.
        /// </summary>
        /// <param name="delay"></param>
        /// <returns></returns>
        ICoreServerOptions ThrottleWebsocketStreaming(TimeSpan delay);

        /// <summary>
        /// Disables all throttling options.
        /// </summary>
        /// <param name="delay"></param>
        /// <returns></returns>
        ICoreServerOptions DisableAllThrottling();

        /// <summary>
        /// Sets a delay for receiving websocket messages from a client.
        /// </summary>
        /// <param name="delay"></param>
        /// <returns></returns>
        ICoreServerOptions ThrottleWebsocketReceiveLoop(TimeSpan delay);

        ICoreServerOptions UseGlobalRouteHandlerBuilder(Action<RouteHandlerBuilder> configure);
        ICoreServerOptions UseGlobalHttpConfigurations(Action<IEndpointConventionBuilder> configure);
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
        /// Delay for ingest messages in the websocket channel.
        /// </summary>
        TimeSpan IngestThrottleDelay { get; }

        /// <summary>
        /// Delay for websocket methods in the websocket channel.
        /// </summary>
        TimeSpan MethodThrottleDelay { get; }

        /// <summary>
        /// Delay for websocket subscription messages.
        /// </summary>
        TimeSpan SubscriptionThrottleDelay { get; }

        /// <summary>
        /// Delay for websocket streaming messages.
        /// </summary>
        TimeSpan StreamingThrottleDelay { get; }

        /// <summary>
        /// Delay for websocket client receive loop.
        /// </summary>
        TimeSpan WebsocketReceiveThrottleDelay { get; }

        /// <summary>
        /// Delay for websocket client receive loop.
        /// </summary>
        bool ThrottlingIsDisabled { get; }

        /// <summary>
        /// Allows configuring extra some global settings for HTTP endpoints.
        /// </summary>
        Action<IEndpointConventionBuilder>? EndpointConventions { get; }

        /// <summary>
        /// Allows configuring extra some global settings for HTTP endpoints.
        /// </summary>
        Action<RouteHandlerBuilder>? RouteHandlerBuilderConfig { get; }
    }
}