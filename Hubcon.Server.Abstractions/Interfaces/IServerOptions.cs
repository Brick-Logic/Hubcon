using System.Reflection;
using Hubcon.Shared.Abstractions.Standard.Interfaces;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;

namespace Hubcon.Server.Abstractions.Interfaces
{
    /// <summary>
    /// Defines configuration options for the Hubcon server instance.
    /// Provides methods to register controllers, middlewares, transports, and essential services.
    /// </summary>
    public interface IServerOptions
    {
        /// <summary>
        /// Registers a global middleware that will be executed for all incoming requests.
        /// </summary>
        /// <typeparam name="T">The type of the middleware to register.</typeparam>
        public void AddGlobalMiddleware<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>();

        /// <summary>
        /// Registers a global middleware of the specified type that will be executed for all incoming requests.
        /// </summary>
        /// <param name="middlewareType">The <see cref="Type"/> of the middleware.</param>
        public void AddGlobalMiddleware(Type middlewareType);

        /// <summary>
        /// Adds and configures a specific controller to the server using a generic type.
        /// </summary>
        /// <typeparam name="T">The type of the controller that implements <see cref="IControllerContract"/>.</typeparam>
        /// <param name="options">A delegate to configure the <see cref="IControllerOptions"/> for the controller.</param>
        public void AddController<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods)] T>(Action<IControllerOptions>? options = null) where T : class, IControllerContract, new();

        /// <summary>
        /// Adds and configures a specific controller to the server using its type.
        /// </summary>
        /// <param name="controllerType">The <see cref="Type"/> of the controller.</param>
        /// <param name="options">A delegate to configure the <see cref="IControllerOptions"/> for the controller.</param>
        public void AddController(Type controllerType, Action<IControllerOptions>? options = null);

        /// <summary>
        /// Configures the internal, low-level core options of the server.
        /// </summary>
        /// <param name="coreServerOptions">A delegate to configure <see cref="ICoreServerOptions"/>.</param>
        public void ConfigureCore(Action<ICoreServerOptions> coreServerOptions);

        /// <summary>
        /// Adds the authentication middleware to the pipeline.
        /// </summary>
        public void AddAuthentication();

        /// <summary>
        /// Scans loaded assemblies and automatically registers all controllers that implement the required interfaces.
        /// </summary>
        public void AutoRegisterControllers();

        /// <summary>
        /// Scans the provided assembly and automatically registers all Hubcon controllers.
        /// </summary>
        public void RegisterControllersFromAssembly(Assembly assembly);

        /// <summary>
        /// Adds and configures a rate limiter for HTTP requests.
        /// </summary>
        /// <param name="options">A delegate to configure the <see cref="RateLimiterOptions"/>.</param>
        public void AddHttpRateLimiter(Action<RateLimiterOptions> options);

        /// <summary>
        /// Adds the telemetry middleware to collect metrics for server monitoring.
        /// </summary>
        public void AddTelemetry();

        /// <summary>
        /// Adds a custom transport protocol to the server.
        /// </summary>
        /// <typeparam name="T">A type that derives from <see cref="HubconTransportAttribute"/> and has a parameterless constructor.</typeparam>
        public void AddTransport<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>() where T : HubconTransportAttribute, new();

        /// <summary>
        /// Configures the token validation parameters used by the framework.
        /// </summary>
        /// <param name="tokenValidationParameters">An instance of <see cref="TokenValidationParameters"/> defining the validation rules.</param>
        public void UseTokenValidationParameters(TokenValidationParameters tokenValidationParameters);

        /// <summary>
        /// Configures hubcon to use the provided <typeparamref name="T"/> type as the main cache implementation for the <see cref="IGlobalRateLimiterManager"/> service.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public void UseCache<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]T>() where T : class, IOperationCache;
        
        /// <summary>
        /// Add the concurrency limiter middleware to the pipeline
        /// </summary>
        public void AddConcurrencyLimiter();
    }
}