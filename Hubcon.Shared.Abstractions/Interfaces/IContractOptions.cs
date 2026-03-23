using Hubcon.Shared.Abstractions.Enums;
using Hubcon.Shared.Abstractions.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace Hubcon.Shared.Abstractions.Interfaces
{
    /// <summary>
    /// Defines the configuration and operational metadata for a specific service contract.
    /// Manages per-contract overrides for transports, security, and execution hooks, 
    /// as well as the resolution of operation-level options.
    /// </summary>
    public interface IContractOptions
    {
        /// <summary>
        /// Gets the <see cref="Type"/> of the service contract interface.
        /// </summary>
        Type ContractType { get; }

        /// <summary>
        /// Gets a thread-safe dictionary containing specialized options for 
        /// individual operations within this contract, keyed by method name.
        /// </summary>
        ConcurrentDictionary<string, IOperationOptions> OperationOptions { get; }

        /// <summary>
        /// Gets the collection of registered lifecycle hooks (e.g., Pre-Invocation, Post-Invocation) 
        /// specific to this contract.
        /// </summary>
        IReadOnlyDictionary<HookType, Func<IInvocationContext, Task>> Hooks { get; }

        /// <summary>
        /// Gets or sets a value indicating whether remote cancellation is allowed for this contract.
        /// </summary>
        bool? RemoteCancellationIsAllowed { get; }

        /// <summary>
        /// Asynchronously executes a specific lifecycle hook for the given invocation context.
        /// </summary>
        /// <param name="hookType">The category of hook to trigger.</param>
        /// <param name="context">The <see cref="IInvocationContext"/> containing state for the current call.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous hook execution.</returns>
        Task CallHook(HookType hookType, IInvocationContext context);

        /// <summary>
        /// Resolves or creates the <see cref="IOperationOptions"/> for a specific method in the contract.
        /// </summary>
        /// <param name="operationName">The name of the operation.</param>
        /// <param name="memberInfo">The reflection metadata for the specific method or property.</param>
        /// <returns>The <see cref="IOperationOptions"/> associated with the member.</returns>
        IOperationOptions GetOperationOptions(string operationName, MemberInfo memberInfo);

        /// <summary>
        /// Gets or sets the preferred transport protocol for all operations in this contract.
        /// If <see langword="null"/>, the client-wide default is applied.
        /// </summary>
        HubconTransportAttribute? TransportType { get; }

        /// <summary>
        /// Gets or sets a value indicating whether authentication is required for this contract.
        /// If <see langword="null"/>, follows the global client authentication policy.
        /// </summary>
        bool? AuthIsEnabled { get; }

        /// <summary>
        /// Gets a dictionary of dynamic header providers that are specific to this contract's operations.
        /// </summary>
        Dictionary<string, Func<IServiceProvider, string>> HeaderProviders { get; }
    }
}
