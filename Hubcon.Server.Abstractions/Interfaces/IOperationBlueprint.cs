using Hubcon.Server.Abstractions.Enums;
using Microsoft.AspNetCore.Authorization;
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
        MemberInfo? OperationInfo { get; }
        string OperationName { get; }
        OperationKind Kind { get; }
        Type[] ParameterTypes { get; }
        Type RawReturnType { get; }
        bool RequiresAuthorization { get; }
        Type ReturnType { get; }
        Func<object?, object[], object?>? InvokeDelegate { get; }
        IPipelineBuilder PipelineBuilder { get; }
        string Route { get; }
    }
}