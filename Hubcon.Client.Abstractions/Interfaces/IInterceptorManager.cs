using Hubcon.Shared.Abstractions.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Hubcon.Client.Abstractions.Interfaces
{
    /// <summary>
    /// Defines the manager responsible for coordinating hooks and interceptors 
    /// across the client, contract, and operation scopes.
    /// </summary>
    public interface IInterceptorManager
    {
        /// <summary>Gets the scoped service provider for resolving dependencies during interception.</summary>
        IServiceProvider ServiceProvider { get; }

        /// <summary>Gets the global configuration options for the Hubcon client.</summary>
        IClientOptions ClientOptions { get; }

        /// <summary>Gets the specific configuration options for the current service contract.</summary>
        IContractOptions ContractOptions { get; }

        /// <summary>Gets the configuration options for the specific operation, if available.</summary>
        IOperationOptions? OperationOptions { get; }

        /// <summary>Gets the current request data being processed, if applicable.</summary>
        IOperationRequest? Request { get; }

        /// <summary>
        /// Executes system-level hooks associated with a specific lifecycle stage.
        /// </summary>
        Task CallHooks(HookType hookType, CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes both system hooks and user-defined interceptors for a lifecycle stage.
        /// </summary>
        Task CallHooksAndInterceptors(HookType hookType, CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes only user-defined interceptors of a specific type.
        /// </summary>
        Task CallInterceptor(InterceptorType interceptorType, CancellationToken cancellationToken = default);

        /// <summary>
        /// Specifically triggers hooks dedicated to request validation logic.
        /// </summary>
        Task CallValidationHooks(CancellationToken cancellationToken = default);
    }
}
