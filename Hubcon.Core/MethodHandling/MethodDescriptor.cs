using Hubcon.Core.Extensions;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Core.MethodHandling
{
    public class MethodDescriptor
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