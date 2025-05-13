using Hubcon.Core.Abstractions.Enums;
using Microsoft.AspNetCore.Authorization;
using System.Reflection;

namespace Hubcon.Core.Abstractions.Interfaces
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
        Delegate? InvokeDelegate { get; }
        IPipelineBuilder PipelineBuilder { get; }
    }
}