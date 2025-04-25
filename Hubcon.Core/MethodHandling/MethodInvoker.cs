using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Core.MethodHandling
{
    public class HubconMethodInvoker
    {
        public string MethodSignature { get; }
        public string ContractName { get; }
        public Type ContractType { get; }
        public Type ControllerType { get; }
        public MethodInfo InternalMethodInfo { get; }
        public MethodInvokerDelegate? Method { get; }
        public Type[] ParameterTypes { get; }
        public Type ReturnType { get; }

        public HubconMethodInvoker(string methodName, MethodInfo internalMethodInfo, MethodInvokerDelegate? method, Type contractType, Type controllerType)
        {
            MethodSignature = methodName;
            InternalMethodInfo = internalMethodInfo;
            Method = method;
            ParameterTypes = internalMethodInfo.GetParameters().Select(x => x.ParameterType).ToArray();
            ReturnType = internalMethodInfo.ReturnType;
            ContractType = contractType;
            ContractName = ContractType.Name;
            ControllerType = controllerType;
        }
    }
}
