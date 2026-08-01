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
        /// Disables all configured rate limiters on the server.
        /// </summary>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        ICoreServerOptions DisableAllRateLimiters();

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
        /// Adds a new setting to the server, accessible from the <see cref="IInternalServerOptions"/> service.
        /// </summary>
        /// <param name="key">The key identifier for the setting.</param>
        /// <param name="value">The value of the setting.</param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance for fluent configuration.</returns>
        ICoreServerOptions AddSetting(string key, object? value);
        
        /// <summary>
        /// Defines settings for a transport.
        /// </summary>
        /// <param name="configurator">A delegate to configure the settings of the transport.</param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        public ICoreServerOptions ConfigureTransport<TAttribute>(Action<ITransportSettingsSetter> configurator)
            where TAttribute : HubconTransportAttribute, new();
        
        /// <summary>
        /// Defines settings for a transport.
        /// </summary>
        /// <param name="configurator">A delegate to configure the settings of the transport.</param>
        /// <returns>The current <see cref="ICoreServerOptions"/> instance, allowing method chaining.</returns>
        public ICoreServerOptions ConfigureTransport<TAttribute, TSettings>(Action<TSettings> configurator) 
            where TAttribute: HubconTransportAttribute<TSettings>, new()
            where TSettings: class, ITransportSettings, new ();
    }

    /// <summary>
    /// Defines the internal configuration and runtime constraints for the Hubcon server.
    /// This interface provides read-only access to protocol-specific limits, timeouts, and feature flags.
    /// </summary>
    public interface IInternalServerOptions
    {
        /// <summary>
        /// Gets a value indicating whether responses should include detailed exception information and stack traces.
        /// </summary>
        public bool DetailedErrorsEnabled { get; }

        /// <summary>
        /// Gets a delegate used to configure additional global metadata or conventions for HTTP endpoints.
        /// </summary>
        public Action<IEndpointConventionBuilder>? EndpointConventions { get; }

        /// <summary>
        /// Gets a delegate used to configure the <see cref="RouteHandlerBuilder"/> for HTTP-based routes.
        /// </summary>
        public Action<RouteHandlerBuilder>? RouteHandlerBuilderConfig { get; }

        /// <summary>
        /// Gets a read-only dictionary mapping transport types to their associated <see cref="HubconTransportAttribute"/>.
        /// </summary>
        IReadOnlyDictionary<Type, HubconTransportAttribute> DefaultTransports { get; }

        /// <summary>
        /// Gets the global rate limiting configuration applied to the entire server instance.
        /// </summary>
        TokenBucketRateLimiterOptions GlobalRateLimiterOptions { get; }
        
        /// <summary>
        /// Gets a value indicating whether request throttling is disabled for the server.
        /// </summary>
        public bool ThrottlingIsDisabled { get; }
        
        /// <summary>
        /// Gets a read-only dictionary that maps transport attributes to their corresponding authentication handler types.
        /// </summary>
        /// <remarks>
        /// Use this property to retrieve the authentication handler type associated with a specific transport attribute, 
        /// enabling dynamic selection of authentication logic based on the transport method in use.
        /// </remarks>
        IReadOnlyDictionary<HubconTransportAttribute, Type> AuthHandlerTypes { get; }
        
        /// <summary>
        /// Gets a read-only dictionary that maps transports to their max concurrent connection values.
        /// </summary>
        IReadOnlyDictionary<HubconTransportAttribute, ITransportSettings> TransportSettings { get; }
        
        /// <summary>
        /// Gets a read-only dictionary for external settings.
        /// </summary>
        /// <remarks>
        /// Use this property to transport server settings to any part of the application.
        /// </remarks>
        IReadOnlyDictionary<string, object?> ExternalSettings { get; }

        /// <summary>
        /// Gets the transport settings that match with the provided transport attribute.
        /// </summary>
        public TSettings GetTransportSettings<TAttribute, TSettings>()
            where TAttribute : HubconTransportAttribute<TSettings>, new()
            where TSettings : class, ITransportSettings, new();

        /// <summary>
        /// Gets the transport settings that match with the provided transport attribute.
        /// </summary>
        public ITransportSettings GetTransportSettings<TAttribute>()
            where TAttribute : HubconTransportAttribute, new();
        
        /// <summary>
        /// Gets the transport settings that match with the provided transport attribute.
        /// </summary>
        public ITransportSettings GetTransportSettings(HubconTransportAttribute transport);
        
        /// <summary>
        /// Gets the transport settings that match with the provided transport attribute.
        /// </summary>
        public TSettings GetTransportSettings<TSettings>(HubconTransportAttribute<TSettings> transport)
            where TSettings : class, ITransportSettings, new();
    }
}