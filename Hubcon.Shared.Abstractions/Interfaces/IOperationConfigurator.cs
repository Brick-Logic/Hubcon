using Hubcon.Shared.Abstractions.Enums;
using Hubcon.Shared.Abstractions.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Hubcon.Shared.Abstractions.Interfaces
{
    /// <summary>
    /// Defines the fluent configuration interface for a specific Hubcon operation.
    /// Allows for granular overrides of transport, security, rate limiting, and lifecycle hooks.
    /// </summary>
    public interface IOperationConfigurator : Hubcon.Shared.Abstractions.Standard.Interfaces.IOperationConfigurator
    {
        /// <summary>
        /// Registers a custom asynchronous hook to be triggered at a specific point in the operation lifecycle.
        /// </summary>
        /// <param name="onSend">The lifecycle stage (e.g., BeforeSend, AfterReceive) that triggers the hook.</param>
        /// <param name="hookDelegate">The asynchronous delegate to execute.</param>
        /// <returns>The current <see cref="IOperationConfigurator"/> instance for method chaining.</returns>
        IOperationConfigurator AddHook(HookType onSend, Func<IInvocationContext, Task> hookDelegate);

        /// <summary>
        /// Adds a validation delegate that is executed before the request is dispatched to the transport.
        /// </summary>
        /// <param name="value">The asynchronous delegate responsible for inspecting and validating the <see cref="RequestValidationContext"/>.</param>
        /// <returns>The current <see cref="IOperationConfigurator"/> instance for method chaining.</returns>
        IOperationConfigurator AddValidationHook(Func<RequestValidationContext, Task> value);

        /// <summary>
        /// Configures a simple rate limiter for this specific operation to throttle the execution frequency.
        /// </summary>
        /// <param name="requestsPerSecond">The maximum number of permits allowed per second.</param>
        /// <param name="rateLimiterIsShared">If <see langword="true"/>, uses a shared bucket across instances; otherwise, uses a dedicated bucket for this operation.</param>
        /// <returns>The current <see cref="IOperationConfigurator"/> instance for method chaining.</returns>
        IOperationConfigurator LimitPerSecond(int requestsPerSecond, bool rateLimiterIsShared = true);

        /// <summary>
        /// Explicitly sets the transport protocol (e.g., WebSocket, HTTP) for this operation via a transport attribute.
        /// </summary>
        /// <typeparam name="T">The type of <see cref="HubconTransportAttribute"/> to apply.</typeparam>
        /// <returns>The current <see cref="IOperationConfigurator"/> instance for method chaining.</returns>
        IOperationConfigurator UseTransport<T>() where T : HubconTransportAttribute, new();

        /// <summary>
        /// Configures whether the client-side <see cref="CancellationToken"/> should be propagated to the server to cancel remote execution.
        /// </summary>
        /// <param name="value"><see langword="true"/> to enable remote cancellation; <see langword="false"/> to ignore client-side cancellation on the server.</param>
        /// <returns>The current <see cref="IOperationConfigurator"/> instance for method chaining.</returns>
        IOperationConfigurator AllowRemoteCancellation(bool value = true);

        /// <summary>
        /// Disables authentication requirements specifically for this operation, overriding higher-level security policies.
        /// </summary>
        /// <returns>The current <see cref="IOperationConfigurator"/> instance for method chaining.</returns>
        IOperationConfigurator DisableHttpAuthentication();

        /// <summary>
        /// Configures advanced rate limiting behavior using a specialized <see cref="RateLimitAttribute"/>.
        /// </summary>
        /// <param name="rateLimitAttribute">The attribute containing complex rate-limiting rules (e.g., burst size, replenishment rate).</param>
        /// <returns>The current <see cref="IOperationConfigurator"/> instance for method chaining.</returns>
        IOperationConfigurator ConfigureRateBucket(RateLimitAttribute rateLimitAttribute);

        /// <summary>
        /// Forces the operation to use the WebSocket transport layer.
        /// </summary>
        /// <returns>The current <see cref="IOperationConfigurator"/> instance for method chaining.</returns>
        IOperationConfigurator UseWebSockets();

        /// <summary>
        /// Forces the operation to use the standard Hubcon HTTP transport layer.
        /// </summary>
        /// <returns>The current <see cref="IOperationConfigurator"/> instance for method chaining.</returns>
        IOperationConfigurator UseHttp();

        /// <summary>
        /// Configures the operation to use a non-standard HTTP transport, typically used for external REST APIs 
        /// that do not follow the Hubcon response envelope structure.
        /// </summary>
        /// <returns>The current <see cref="IOperationConfigurator"/> instance for method chaining.</returns>
        IOperationConfigurator UseNonHubconHttp();

        /// <summary>
        /// Registers a dynamic header provider for this specific operation.
        /// </summary>
        /// <param name="key">The HTTP header name.</param>
        /// <param name="valueProvider">A delegate that resolves the header value from the <see cref="IServiceProvider"/> at runtime.</param>
        /// <returns>The current <see cref="IOperationConfigurator"/> instance for method chaining.</returns>
        IOperationConfigurator AddHeaderProvider(string key, Func<IServiceProvider, string> valueProvider);
    }
}