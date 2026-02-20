using Hubcon.Shared.Abstractions.Enums;
using Hubcon.Shared.Abstractions.Models;
using System;
using System.Threading.Tasks;

namespace Hubcon.Shared.Abstractions.Interfaces
{
    /// <summary>
    /// Defines methods for configuring a contract, including transport settings, operations, hooks,
    /// cancellation, communication protocols, headers, and authentication.
    /// </summary>
    /// <returns>The current instance of <see cref="IContractConfigurator{T}"/> for method chaining.</returns>
    public interface IContractConfigurator<T>
    {
        /// <summary>
        /// Sets the default transport method for the contract.
        /// </summary>
        /// <typeparam name="TTransport">The transport attribute type, deriving from <see cref="HubconTransportAttribute"/>.</typeparam>
        /// <returns>The current instance of <see cref="IContractConfigurator{T}"/> for method chaining.</returns>
        public IContractConfigurator<T> SetDefaultTransport<TTransport>() where TTransport : HubconTransportAttribute, new();

        /// <summary>
        /// Configures the operations for the contract using an action delegate.
        /// </summary>
        /// <param name="selector">An action to configure operation selection.</param>
        /// <returns>The current instance of <see cref="IContractConfigurator{T}"/> for method chaining.</returns>
        public IContractConfigurator<T> ConfigureOperations(Action<IOperationSelector<T>> selector);

        /// <summary>
        /// Adds a hook to the contract that will be invoked during specific operations. Note that the same hook type cannot be registered multiples times.
        /// </summary>
        /// <param name="hookType">The type of hook to add, defined by <see cref="HookType"/>.</param>
        /// <param name="hookDelegate">The asynchronous delegate to invoke when the hook is triggered.</param>
        /// <returns>The current instance of <see cref="IContractConfigurator{T}"/> for method chaining.</returns>
        public IContractConfigurator<T> AddHook(HookType hookType, Func<IInvocationContext, Task> hookDelegate);

        /// <summary>
        /// Enables or disables remote cancellation for the contract.
        /// </summary>
        /// <param name="value">True to allow remote cancellation; otherwise, false.</param>
        /// <returns>The current instance of <see cref="IContractConfigurator{T}"/> for method chaining.</returns>
        public IContractConfigurator<T> AllowRemoteCancellation(bool value = true);

        /// <summary>
        /// Configures the contract to use WebSockets for communication.
        /// </summary>
        /// <returns>The current instance of <see cref="IContractConfigurator{T}"/> for method chaining.</returns>
        public IContractConfigurator<T> UseWebSockets();

        /// <summary>
        /// Configures the contract to use HTTP for communication.
        /// </summary>
        /// <returns>The current instance of <see cref="IContractConfigurator{T}"/> for method chaining.</returns>
        public IContractConfigurator<T> UseHttp();

        /// <summary>
        /// Configures the contract to use non-HubCon HTTP.
        /// </summary>
        /// <returns>The current instance of <see cref="IContractConfigurator{T}"/> for method chaining.</returns>
        public IContractConfigurator<T> UseNonHubconHttp();

        /// <summary>
        /// Adds a header provider function that supplies headers for requests.
        /// </summary>
        /// <param name="key">The header key.</param>
        /// <param name="valueProvider">A function that provides the header value given an <see cref="IServiceProvider"/>.</param>
        /// <returns>The current instance of <see cref="IContractConfigurator{T}"/> for method chaining.</returns>
        public IContractConfigurator<T> AddHeaderProvider(string key, Func<IServiceProvider, string> valueProvider);

        /// <summary>
        /// Enables or disables authentication for the contract.
        /// </summary>
        /// <param name="enabled">True to enable authentication; otherwise, false.</param>
        /// <returns>The current instance of <see cref="IContractConfigurator{T}"/> for method chaining.</returns>
        public IContractConfigurator<T> EnableAuth(bool enabled);
        IOperationConfigurator ForOperation<TDelegate>(System.Linq.Expressions.Expression<Func<T, TDelegate>> expression);
    }
}
