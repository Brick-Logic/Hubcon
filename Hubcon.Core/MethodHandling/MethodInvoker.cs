using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Core.MethodHandling
{
    internal class MethodInvoker
    {
        public string MethodSignature { get; set; }
        public MethodInfo InternalMethodInfo { get; set; }
        public MethodInvokerDelegate? Method { get; set; }
        public Type[] ParameterTypes { get; set; }
        public Type ReturnType { get; set; }

        public MethodInvoker(string methodName, MethodInfo internalMethodInfo, MethodInvokerDelegate? method)
        {
            MethodSignature = methodName;
            InternalMethodInfo = internalMethodInfo;
            Method = method;
            ParameterTypes = internalMethodInfo.GetParameters().Select(x => x.ParameterType).ToArray();
            ReturnType = internalMethodInfo.ReturnType;
        }
    }
}
