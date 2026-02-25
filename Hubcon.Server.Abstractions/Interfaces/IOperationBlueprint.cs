using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Reflection;

namespace Hubcon
{
    /// <summary>
    /// Represents the blueprint of an operation, providing metadata and configuration details to facilitate its execution within the system.
    /// </summary>
    public interface IOperationBlueprint
    {
        /// <summary>
        /// Gets the collection of authorization attributes associated with the operation.
        /// </summary>
        IEnumerable<AuthorizeAttribute> AuthorizationAttributes { get; }

        /// <summary>
        /// Gets the name of the contract associated with the operation.
        /// </summary>
        string ContractName { get; }

        /// <summary>
        /// Gets the type of the contract for the operation.
        /// </summary>
        Type ContractType { get; }

        /// <summary>
        /// Gets the name of the controller handling the operation.
        /// </summary>
        string ControllerName { get; }

        /// <summary>
        /// Gets the type of the controller handling the operation.
        /// </summary>
        Type ControllerType { get; }

        /// <summary>
        /// Indicates whether the operation has a return type.
        /// </summary>
        bool HasReturnType { get; }

        /// <summary>
        /// Gets the name of the HTTP endpoint group associated with the operation, if any.
        /// </summary>
        string? HttpEndpointGroupName { get; }

        /// <summary>
        /// Gets metadata information about the member defining the operation.
        /// </summary>
        MemberInfo? MemberInfo { get; }

        /// <summary>
        /// Gets the name of the operation.
        /// </summary>
        string OperationName { get; }

        /// <summary>
        /// Gets the type of the operation, representing its kind.
        /// </summary>
        OperationKind Kind { get; }

        /// <summary>
        /// Gets the parameter types associated with the operation.
        /// </summary>
        ConcurrentDictionary<string, Type> ParameterTypes { get; }

        /// <summary>
        /// Gets the raw return type of the operation.
        /// </summary>
        Type RawReturnType { get; }

        /// <summary>
        /// Gets a value indicating whether the operation requires authorization.
        /// </summary>
        bool RequiresAuthorization { get; }

        /// <summary>
        /// Gets the specific return type of the operation.
        /// </summary>
        Type ReturnType { get; }

        /// <summary>
        /// Gets the delegate used to invoke the operation.
        /// </summary>
        Func<object?, object, object?>? InvokeDelegate { get; }

        /// <summary>
        /// Gets the pipeline builder used for the operation.
        /// </summary>
        IPipelineBuilder PipelineBuilder { get; }

        /// <summary>
        /// Gets the optional call wrapper type for the operation.
        /// </summary>
        Type? CallWrapperType { get; }

        /// <summary>
        /// Gets the HTTP route associated with the operation, if any.
        /// </summary>
        string? HttpRoute { get; }

        /// <summary>
        /// Gets the collection of configuration attributes associated with the operation.
        /// </summary>
        ConcurrentDictionary<Type, Attribute> ConfigurationAttributes { get; }

        /// <summary>
        /// Gets the collection of transport attributes associated with the operation.
        /// </summary>
        ConcurrentDictionary<Type, Attribute> TransportAttributes { get; }

        /// <summary>
        /// Gets the list of all attributes associated with the operation.
        /// </summary>
        IList<Attribute> Attributes { get; }

        /// <summary>
        /// Gets the set of precomputed roles for the operation.
        /// </summary>
        HashSet<string> PrecomputedRoles { get; }

        /// <summary>
        /// Gets the array of precomputed policies for the operation.
        /// </summary>
        HashSet<string> PrecomputedPolicies { get; }

        /// <summary>
        /// Gets the simplified name of the contract.
        /// </summary>
        string SimpleContractName { get; }
        
        /// <summary>
        /// Gets the delegate responsible for mapping wrappers.
        /// </summary>
        Action<IDictionary<string, object>, object, CancellationToken>? WrapperMapper { get; }

        /// <summary>
        /// Gets the HTTP method used for the operation, if any.
        /// </summary>
        HttpMethod? HttpVerb { get; }

        /// <summary>
        /// Indicates whether the operation has any subscriptions.
        /// </summary>
        bool HasSubscriptions { get; }

        /// <summary>
        /// Gets or sets the factory used to create controller instances.
        /// </summary>
        ObjectFactory ControllerFactory { get; }
        CompiledSecurityPolicy SecurityPolicy { get; }

        /// <summary>
        /// Determines whether a specific transport attribute type is supported by the operation.
        /// </summary>
        /// <typeparam name="T">The type of transport attribute to check.</typeparam>
        /// <returns>True if the transport type is supported; otherwise, false.</returns>
        public bool SupportsTransport<T>() where T : HubconTransportAttribute
        {
            if (TransportAttributes.IsEmpty)
                return true;

            return TransportAttributes.Any(x => x.Value is T);
        }
    }
}