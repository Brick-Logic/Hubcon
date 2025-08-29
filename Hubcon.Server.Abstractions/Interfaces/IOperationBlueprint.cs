using Hubcon.Server.Abstractions.Enums;
using Microsoft.AspNetCore.Authorization;
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
        string HttpEndpointGroupName { get; }
        MemberInfo? OperationInfo { get; }
        string OperationName { get; }
        OperationKind Kind { get; }
        Dictionary<string, Type> ParameterTypes { get; }
        Type RawReturnType { get; }
        bool RequiresAuthorization { get; }
        Type ReturnType { get; }
        Func<object?, object[], object?>? InvokeDelegate { get; }
        IPipelineBuilder PipelineBuilder { get; }
        string Route { get; }
        ConcurrentDictionary<Type, Attribute> ConfigurationAttributes { get; }
        IEnumerable<Attribute> Attributes { get; }
    }
}