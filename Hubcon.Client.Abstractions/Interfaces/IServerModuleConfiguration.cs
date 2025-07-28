using Hubcon.Shared.Abstractions.Standard.Interfaces;
using Hubcon.Shared.Abstractions.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System.Net.WebSockets;

namespace Hubcon.Client.Abstractions.Interfaces
{
    public interface IServerModuleConfiguration
    {
        /// <summary>
        /// Registers a contract interface with optional configuration.
        /// </summary>
        /// <typeparam name="T">The controller contract type.</typeparam>
        /// <param name="configure">Optional configuration action for the contract.</param>
        /// <returns>The current server module configuration instance.</returns>
        IServerModuleConfiguration Implements<T>(Action<IContractConfigurator<T>>? configure = null) where T : IControllerContract;

        /// <summary>
        /// Specifies the authentication manager to use for the server module. The authentication manager will only be used for this module.
        /// </summary>
        /// <typeparam name="T">The authentication manager type.</typeparam>
        /// <returns>The current server module configuration instance.</returns>
        IServerModuleConfiguration UseAuthenticationManager<T>() where T : class, IAuthenticationManager;

        /// <summary>
        /// Sets the base URL that the contracts will use to connect to the server.
        /// </summary>
        /// <param name="baseUrl">The base URL string.</param>
        /// <returns>The current server module configuration instance.</returns>
        IServerModuleConfiguration WithBaseUrl(string baseUrl);

        /// <summary>
        /// Configures the server module to use an insecure connection (e.g., HTTP instead of HTTPS, WS instead of WSS).
        /// </summary>
        /// <returns>The current server module configuration instance.</returns>
        IServerModuleConfiguration UseInsecureConnection();

        /// <summary>
        /// Sets the HTTP prefix for requests.
        /// </summary>
        /// <param name="prefix">The HTTP prefix string.</param>
        /// <returns>The current server module configuration instance.</returns>
        IServerModuleConfiguration WithHttpPrefix(string prefix);

        /// <summary>
        /// Sets the WebSocket endpoint that the client should use to connect to the server.
        /// </summary>
        /// <param name="endpoint">The WebSocket endpoint string.</param>
        /// <returns>The current server module configuration instance.</returns>
        IServerModuleConfiguration WithWebsocketEndpoint(string endpoint);

        /// <summary>
        /// Configures the WebSocket client options.
        /// </summary>
        /// <param name="options">An action to configure <see cref="ClientWebSocketOptions"/>.</param>
        /// <returns>The current server module configuration instance.</returns>
        IServerModuleConfiguration ConfigureWebsocketClient(Action<ClientWebSocketOptions> options);

        /// <summary>
        /// Configures the HTTP client options.
        /// </summary>
        /// <param name="options">An action to configure <see cref="HttpClient"/>.</param>
        /// <returns>The current server module configuration instance.</returns>
        IServerModuleConfiguration ConfigureHttpClient(Action<HttpClient> options);

        /// <summary>
        /// Sets the interval for sending WebSocket ping messages.
        /// </summary>
        /// <param name="timeSpan">The ping interval.</param>
        /// <returns>The current server module configuration instance.</returns>
        IServerModuleConfiguration SetWebsocketPingInterval(TimeSpan timeSpan);

        /// <summary>
        /// Specifies whether a pong response is required for WebSocket pings.
        /// </summary>
        /// <param name="value">True to require pong response; otherwise, false.</param>
        /// <returns>The current server module configuration instance.</returns>
        IServerModuleConfiguration RequirePongResponse(bool value);

        /// <summary>
        /// Sets the number of message processors to scale to. The message processors are used to handle incoming messages. 
        /// Scaling them allows for better performance and concurrency in processing messages. 
        /// Do not set this value too high, as it may lead to performance degradation due to excessive context switching.
        /// Default value is 1.
        /// </summary>
        /// <param name="count">The number of processors.</param>
        /// <returns>The current server module configuration instance.</returns>
        IServerModuleConfiguration ScaleMessageProcessors(int count);

        /// <summary>
        /// Enables or disables automatic websocket reconnection for the server module.
        /// </summary>
        /// <param name="value">True to enable auto reconnect; otherwise, false. Default is true.</param>
        /// <returns>The current server module configuration instance.</returns>
        IServerModuleConfiguration EnableWebsocketAutoReconnect(bool value = true);

        /// <summary>
        /// Enables or disables automatic reconnection for stream connections.
        /// </summary>
        /// <param name="value">True to enable reconnecting streams; otherwise, false. Default is true.</param>
        /// <returns>The current server module configuration instance.</returns>
        IServerModuleConfiguration ResubcribeStreamingOnReconnect(bool value = true);

        /// <summary>
        /// Enables or disables automatic reconnection for subscriptions.
        /// </summary>
        /// <param name="value">True to enable reconnecting subscriptions; otherwise, false. Default is true.</param>
        /// <returns>The current server module configuration instance.</returns>
        IServerModuleConfiguration ResubscribeOnReconnect(bool value = true);

        /// <summary>
        /// Enables or disables automatic reconnection for ingest connections.
        /// </summary>
        /// <param name="value">True to enable reconnecting ingests; otherwise, false. Default is true.</param>
        /// <returns>The current server module configuration instance.</returns>
        IServerModuleConfiguration ResubscribeIngestOnReconnect(bool value = true);
    }
}