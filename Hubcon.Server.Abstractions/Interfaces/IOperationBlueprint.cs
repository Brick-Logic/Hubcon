using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Reflection;

namespace Hubcon
{
    public interface IOperationBlueprint
    {
        IEnumerable<AuthorizeAttribute> AuthorizationAttributes { get; }
        string ContractName { get; }
        Type ContractType { get; }
        string ControllerName { get; }
        Type ControllerType { get; }
        bool HasReturnType { get; }
        string? HttpEndpointGroupName { get; }
        MemberInfo? MemberInfo { get; }
        string OperationName { get; }
        OperationKind Kind { get; }
        ConcurrentDictionary<string, Type> ParameterTypes { get; }
        Type RawReturnType { get; }
        bool RequiresAuthorization { get; }
        Type ReturnType { get; }
        Func<object?, object, object?>? InvokeDelegate { get; }
        IPipelineBuilder PipelineBuilder { get; }
        Type? CallWrapperType { get; }
        string? HttpRoute { get; }
        ConcurrentDictionary<Type, Attribute> ConfigurationAttributes { get; }
        ConcurrentDictionary<Type, Attribute> TransportAttributes { get; }
        IList<Attribute> Attributes { get; }
        HashSet<string> PrecomputedRoles { get; }
        string?[] PrecomputedPolicies { get; }
        string SimpleContractName { get; }
        Action<IDictionary<string, object>, object, CancellationToken>? WrapperMapper { get; }
        HttpMethod? HttpVerb { get; }
        IReadOnlyList<(PropertyInfo PropInfo, Action<object, object?> FastSetter)> SubscriptionProperties { get; }

        bool HasSubscriptions { get; }
        ObjectFactory ControllerFactory { get; }

        public bool SupportsTransport<T>() where T : class, ITransportAttribute
        {
            if (TransportAttributes.IsEmpty)
                return true;

            return TransportAttributes.Any(x => x is T);
        }
    }
}