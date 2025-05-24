using System;
using System.Reflection;
using Castle.DynamicProxy;

namespace Hubcon.Shared.Abstractions.Models
{

    public class SimpleInvocation : IInvocation
    {
        private readonly object _proxy;
        private readonly MethodInfo _method;
        private readonly object?[] _arguments;
        private readonly object _invocationTarget;

        public SimpleInvocation(object proxy, object invocationTarget, MethodInfo method, params object?[] arguments)
        {
            _proxy = proxy;
            _invocationTarget = invocationTarget;
            _method = method;
            _arguments = arguments;
        }

        public object Proxy => _proxy;

        public MethodInfo Method => _method;

        public object[] Arguments => _arguments!;

        public object ReturnValue { get; set; } = null!;

        public Type[] GenericArguments => Type.EmptyTypes;

        public object InvocationTarget => _invocationTarget;

        public MethodInfo MethodInvocationTarget => _method;

        public Type TargetType => _proxy.GetType();

        public void Proceed() { }

        public object GetArgumentValue(int index) => _arguments![index]!;

        public void SetArgumentValue(int index, object value) => _arguments[index] = value;

        public MethodInfo GetConcreteMethod() => _method;

        public MethodInfo GetConcreteMethodInvocationTarget() => _method;

        public IInvocationProceedInfo CaptureProceedInfo() => default!;
    }
}
