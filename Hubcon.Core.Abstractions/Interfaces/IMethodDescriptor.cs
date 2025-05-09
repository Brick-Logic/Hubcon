using Hubcon.Core.Abstractions.Delegates;
using Microsoft.AspNetCore.Authorization;
using System.Reflection;

namespace Hubcon.Core.Abstractions.Interfaces
{
    public interface IMethodDescriptor
    {
        IEnumerable<AuthorizeAttribute> AuthorizationAttributes { get; }
        string ContractName { get; }
        Type ContractType { get; }
        Type ControllerType { get; }
        MethodInfo InternalMethodInfo { get; }
        MethodDelegate? Method { get; }
        string MethodSignature { get; }
        Type[] ParameterTypes { get; }
        bool RequiresAuthorization { get; }
        Type ReturnType { get; }
    }
}