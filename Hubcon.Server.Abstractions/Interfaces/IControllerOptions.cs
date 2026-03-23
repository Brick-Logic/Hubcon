using Hubcon.Shared.Abstractions.Enums;

namespace Hubcon.Server.Abstractions.Interfaces
{
    /// <summary>
    /// Provides configuration options for a specific controller within the Hubcon framework.
    /// Allows for the registration of controller-specific middlewares and execution order settings.
    /// </summary>
    public interface IControllerOptions
    {
        /// <summary>
        /// Adds a middleware to the controller pipeline using a generic type.
        /// </summary>
        /// <typeparam name="T">The type of the middleware that implements <see cref="IMiddleware"/>.</typeparam>
        /// <param name="cycle">The <see cref="MiddlewareLifeCycle"/> that defines the lifetime of the middleware. Defaults to <see cref="MiddlewareLifeCycle.Scoped"/>.</param>
        /// <returns>The current <see cref="IControllerOptions"/> instance for fluent chaining.</returns>
        public IControllerOptions AddMiddleware<T>(MiddlewareLifeCycle cycle = MiddlewareLifeCycle.Scoped) where T : class, IMiddleware;

        /// <summary>
        /// Adds a middleware to the controller pipeline using its <see cref="Type"/>.
        /// </summary>
        /// <param name="middlewareType">The <see cref="Type"/> of the middleware.</param>
        /// <param name="cycle">The <see cref="MiddlewareLifeCycle"/> that defines the lifetime of the middleware. Defaults to <see cref="MiddlewareLifeCycle.Scoped"/>.</param>
        /// <returns>The current <see cref="IControllerOptions"/> instance for fluent chaining.</returns>
        public IControllerOptions AddMiddleware(Type middlewareType, MiddlewareLifeCycle cycle = MiddlewareLifeCycle.Scoped);

        /// <summary>
        /// Sets whether global middlewares should be executed before controller-specific middlewares.
        /// </summary>
        /// <param name="value"><see langword="true"/> to execute global middlewares first; otherwise, <see langword="false"/>. Defaults to <see langword="true"/>.</param>
        /// <returns>The current <see cref="IControllerOptions"/> instance for fluent chaining.</returns>
        public IControllerOptions UseGlobalMiddlewaresFirst(bool value = true);
    }
}