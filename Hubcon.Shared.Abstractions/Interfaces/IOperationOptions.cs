using Hubcon.Shared.Abstractions.Enums;
using Hubcon.Shared.Abstractions.Models;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

namespace Hubcon.Shared.Abstractions.Interfaces
{
    /// <summary>
    /// Defines the final, resolved configuration for a specific service operation.
    /// This interface stores the settings for execution, including transport preferences, 
    /// rate limiting buckets, and per-operation lifecycle hooks.
    /// </summary>
    public interface IOperationOptions
    {
        /// <summary>
        /// Externally added settings.
        /// </summary>
        public IReadOnlyDictionary<string, object?> ExternalSettings { get; }
        
        /// <summary>
        /// Gets the resolved transport protocol (WebSocket/HTTP) for this specific operation.
        /// If null, the operation inherits the transport from the contract or global settings.
        /// </summary>
        HubconTransportAttribute? TransportType { get; }

        /// <summary>
        /// Gets the reflection metadata for the specific method or property this operation represents.
        /// </summary>
        MemberInfo MemberInfo { get; }

        /// <summary>
        /// Gets the type of member (e.g., Method, Property) associated with this operation.
        /// </summary>
        MemberType MemberType { get; }

        /// <summary>
        /// Gets the configuration options for the token bucket rate limiter, if defined.
        /// </summary>
        TokenBucketRateLimiterOptions? RateBucketOptions { get; }

        /// <summary>
        /// Gets the maximum number of requests allowed per second for this operation.
        /// </summary>
        int RequestsPerSecond { get; }

        /// <summary>
        /// Gets a value indicating whether the rate limiter is shared across all instances 
        /// of this operation or is unique to this specific call path.
        /// </summary>
        bool RateLimiterIsShared { get; }

        /// <summary>
        /// Gets the active <see cref="RateLimiter"/> instance used to throttle this operation.
        /// </summary>
        RateLimiter? RateBucket { get; }

        /// <summary>
        /// Gets the collection of registered lifecycle hooks specific to this operation.
        /// </summary>
        IReadOnlyDictionary<HookType, Func<IInvocationContext, Task>> Hooks { get; }

        /// <summary>
        /// Gets a value indicating whether client-side cancellation tokens are propagated 
        /// to the server for this operation.
        /// </summary>
        bool? RemoteCancellationIsAllowed { get; }

        /// <summary>
        /// Gets a value indicating whether authentication is required to execute this operation.
        /// </summary>
        bool? AuthIsEnabled { get; }

        /// <summary>
        /// Gets a dictionary of dynamic header providers that resolve values at runtime 
        /// specifically for this operation's requests.
        /// </summary>
        IReadOnlyDictionary<string, Func<IServiceProvider, string>> HeaderProviders { get; }

        /// <summary>
        /// Asynchronously triggers a specific lifecycle hook for the given invocation context.
        /// </summary>
        /// <param name="hookType">The category of hook to trigger (e.g., BeforeSend).</param>
        /// <param name="context">The <see cref="IInvocationContext"/> containing the current call state.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous hook execution.</returns>
        Task CallHook(HookType hookType, IInvocationContext context);

        /// <summary>
        /// Asynchronously executes all validation logic registered for this operation 
        /// before the request is dispatched.
        /// </summary>
        /// <param name="services">The <see cref="IServiceProvider"/> used to resolve validation dependencies.</param>
        /// <param name="request">The <see cref="IOperationRequest"/> being validated.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to monitor for cancellation.</param>
        /// <returns>A <see cref="Task"/> representing the validation process.</returns>
        Task CallValidationHook(IServiceProvider services, IOperationRequest request, CancellationToken cancellationToken);
    }
}
