using Hubcon.Server.Abstractions.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Reflection;

namespace Hubcon.Server.Abstractions.Interfaces
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
        MemberInfo? OperationInfo { get; }
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
        IEnumerable<Attribute> Attributes { get; }
        HashSet<string> PrecomputedRoles { get; }
        string?[] PrecomputedPolicies { get; }
        string SimpleContractName { get; }
        Action<IDictionary<string, object>, object, CancellationToken>? WrapperMapper { get; }
        HttpMethod? HttpVerb { get; }
        IReadOnlyList<(PropertyInfo PropInfo, Action<object, object?> FastSetter)> SubscriptionProperties { get; }

        bool HasSubscriptions { get; }
        ObjectFactory ControllerFactory { get; }
    }
}