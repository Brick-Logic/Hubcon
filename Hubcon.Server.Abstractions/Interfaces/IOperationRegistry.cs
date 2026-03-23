using Hubcon.Shared.Abstractions.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Hubcon.Server.Abstractions.Interfaces
{
    /// <summary>
    /// Defines a registry for managing and resolving operation blueprints within the Hubcon server.
    /// Responsible for controller discovery, metadata storage, and mapping operations to specific transport protocols.
    /// </summary>
    public interface IOperationRegistry
    {
        /// <summary>
        /// Occurs when a new operation has been successfully resolved and registered in the system.
        /// </summary>
        event Action<IOperationBlueprint>? OnOperationRegistered;

        /// <summary>
        /// Determines whether a specific controller type has already been registered in the registry.
        /// </summary>
        /// <param name="controllerType">The <see cref="Type"/> of the controller to check.</param>
        /// <returns><see langword="true"/> if the controller is registered; otherwise, <see langword="false"/>.</returns>
        bool ControllerExists(Type controllerType);

        /// <summary>
        /// Attempts to retrieve an operation blueprint based on an endpoint request and a specific transport attribute.
        /// </summary>
        /// <param name="request">The <see cref="IOperationEndpoint"/> identifying the target operation.</param>
        /// <param name="transportAttribute">The <see cref="HubconTransportAttribute"/> associated with the current communication channel.</param>
        /// <param name="value">When this method returns, contains the <see cref="IOperationBlueprint"/> if found; otherwise, <see langword="null"/>.</param>
        /// <returns><see langword="true"/> if the blueprint was found; otherwise, <see langword="false"/>.</returns>
        bool TryGetOperationBlueprint(IOperationEndpoint request, HubconTransportAttribute transportAttribute, out IOperationBlueprint? value);

        /// <summary>
        /// Retrieves an operation blueprint using the contract name and operation name for a specific transport.
        /// </summary>
        /// <param name="contractName">The name of the service contract.</param>
        /// <param name="operationName">The name of the specific operation or method.</param>
        /// <param name="transportAttribute">The <see cref="HubconTransportAttribute"/> defining the transport layer.</param>
        /// <param name="value">When this method returns, contains the <see cref="IOperationBlueprint"/> if found; otherwise, <see langword="null"/>.</param>
        /// <returns><see langword="true"/> if the blueprint was found; otherwise, <see langword="false"/>.</returns>
        bool GetOperationBlueprint(string contractName, string operationName, HubconTransportAttribute transportAttribute, out IOperationBlueprint? value);

        /// <summary>
        /// Maps the registered operations to a specific transport protocol within the ASP.NET Core <see cref="WebApplication"/>.
        /// </summary>
        /// <typeparam name="T">The type of <see cref="HubconTransportAttribute"/> to map.</typeparam>
        /// <param name="app">The <see cref="WebApplication"/> instance where the endpoints will be configured.</param>
        /// <param name="endpointRegisterer">An optional delegate to perform custom endpoint registration logic using the resolved blueprints.</param>
        void MapTransport<T>(WebApplication app, Action<IReadOnlyDictionary<string, IOperationBlueprint>, WebApplication>? endpointRegisterer = null) where T : HubconTransportAttribute, new();

        /// <summary>
        /// Scans a controller type for valid Hubcon operations and registers them into the registry.
        /// </summary>
        /// <param name="controllerType">The <see cref="Type"/> of the controller to scan.</param>
        /// <param name="options">Optional configuration settings for the controller.</param>
        /// <param name="serverOptions">The current <see cref="IInternalServerOptions"/> used to validate registration constraints.</param>
        /// <param name="servicesToInject">When this method returns, contains a list of service registration actions required by the discovered operations.</param>
        void RegisterOperations(Type controllerType, Action<IControllerOptions>? options, IInternalServerOptions serverOptions, out List<Action<IServiceCollection>> servicesToInject);
    }
}