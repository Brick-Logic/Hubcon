using Hubcon.Server.Abstractions.Delegates;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Shared.Components.Extensions;
using Microsoft.AspNetCore.Authorization;
using System.Reflection;

namespace Hubcon.Server.Components.Pipelines
{
    public class MethodDescriptor : IMethodDescriptor
    {
        public string MethodSignature { get; }
        public string ContractName { get; }
        public Type ContractType { get; }
        public Type ControllerType { get; }
        public bool RequiresAuthorization { get; }
        public IEnumerable<AuthorizeAttribute> AuthorizationAttributes { get; }
        public MethodInfo InternalMethodInfo { get; }
        public MethodDelegate? Method { get; }
        public Type[] ParameterTypes { get; }
        public Type ReturnType { get; }

        public MethodDescriptor(string methodName, MethodInfo internalMethodInfo, MethodDelegate? method, Type contractType, Type controllerType)
        {
            MethodSignature = methodName;
            InternalMethodInfo = internalMethodInfo;
            Method = method;
            ParameterTypes = internalMethodInfo.GetParameters().Select(x => x.ParameterType).ToArray();
            ReturnType = internalMethodInfo.ReturnType;
            ContractType = contractType;
            ContractName = ContractType.Name;
            ControllerType = controllerType;
            RequiresAuthorization = internalMethodInfo.HasCustomAttribute<AuthorizeAttribute>();

            AuthorizationAttributes = RequiresAuthorization == true
                ? internalMethodInfo.GetCustomAttributes<AuthorizeAttribute>()
                : Array.Empty<AuthorizeAttribute>();
        }
    }
}