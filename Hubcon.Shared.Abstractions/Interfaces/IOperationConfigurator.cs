using Hubcon.Shared.Abstractions.Enums;
using Hubcon.Shared.Abstractions.Models;
using System;
using System.Threading.Tasks;

namespace Hubcon.Shared.Abstractions.Interfaces
{
    public interface IOperationConfigurator : Hubcon.Shared.Abstractions.Standard.Interfaces.IOperationConfigurator
    {
        /// <summary>
        /// Adds a hook to be triggered during the operation lifecycle.
        /// </summary>
        /// <param name="onSend">The type of hook to add, specifying when it should be triggered.</param>
        /// <param name="hookDelegate">The delegate to execute when the hook is triggered.</param>
        /// <returns>The current instance of <see cref="IOperationConfigurator"/> for method chaining.</returns>
        IOperationConfigurator AddHook(HookType onSend, Func<IInvocationContext, Task> hookDelegate);

        /// <summary>
        /// Adds a validation hook to validate the request before execution.
        /// </summary>
        /// <param name="value">The delegate to execute for request validation.</param>
        /// <returns>The current instance of <see cref="IOperationConfigurator"/> for method chaining.</returns>
        IOperationConfigurator AddValidationHook(Func<RequestValidationContext, Task> value);

        /// <summary>
        /// Limits the number of requests per second for the operation.
        /// </summary>
        /// <param name="requestsPerSecond">The maximum number of requests allowed per second.</param>
        /// <param name="rateLimiterIsShared">Indicates whether the rate limiter is shared across operations. Defaults to true.</param>
        /// <returns>The current instance of <see cref="IOperationConfigurator"/> for method chaining.</returns>
        IOperationConfigurator LimitPerSecond(int requestsPerSecond, bool rateLimiterIsShared = true);

        /// <summary>
        /// Specifies the transport type to use for the operation.
        /// </summary>
        /// <param name="transportType">The transport type to use (e.g., HTTP, WebSockets).</param>
        /// <returns>The current instance of <see cref="IOperationConfigurator"/> for method chaining.</returns>
        IOperationConfigurator UseTransport<T>() where T : HubconTransportAttribute, new();

        /// <summary>
        /// Allows or disallows remote cancellation of the operation.
        /// </summary>
        /// <param name="value">True to allow remote cancellation; false to disallow. Defaults to true.</param>
        /// <returns>The current instance of <see cref="IOperationConfigurator"/> for method chaining.</returns>
        IOperationConfigurator AllowRemoteCancellation(bool value = true);

        /// <summary>
        /// Disables HTTP authentication for the operation.
        /// </summary>
        /// <returns>The current instance of <see cref="IOperationConfigurator"/> for method chaining.</returns>
        IOperationConfigurator DisableHttpAuthentication();
        IOperationConfigurator ConfigureRateBucket(RateLimitAttribute rateLimitAttribute);
        IOperationConfigurator UseWebSockets();
        IOperationConfigurator UseHttp();
        IOperationConfigurator UseNonHubconHttp();
        IOperationConfigurator AddHeaderProvider(string key, Func<string> valueProvider);
    }
}