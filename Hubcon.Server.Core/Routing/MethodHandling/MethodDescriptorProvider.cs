using Hubcon.Server.Abstractions.Delegates;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Core.Pipelines;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Standard.Interfaces;
using Hubcon.Shared.Core.Extensions;
using System.Linq.Expressions;
using System.Reflection;

namespace Hubcon.Server.Core.Routing.MethodHandling
{
    public class MethodDescriptorProvider : IMethodDescriptorProvider
    {
        public event Action<IMethodDescriptor>? OnMethodRegistered;

        internal Dictionary<string, Dictionary<string, IMethodDescriptor>> ControllerMethods = new();

        public void RegisterMethods(Type controllerType)
        {
            if (!typeof(IControllerContract).IsAssignableFrom(controllerType))
                throw new NotImplementedException($"El tipo {controllerType.FullName} no implementa la interfaz {nameof(IControllerContract)} o un tipo derivado.");

            var interfaces = controllerType.GetInterfaces().Where(x => typeof(IControllerContract).IsAssignableFrom(x));

            foreach (var interfaceType in interfaces)
            {
                if (interfaceType.GetMethods().Length == 0)
                    continue;

                if (!ControllerMethods.TryGetValue(interfaceType.Name, out Dictionary<string, IMethodDescriptor>? contractMethods))
                    contractMethods = ControllerMethods[interfaceType.Name] = new();

                if (contractMethods.Count > 0)
                    continue;

                foreach (var method in interfaceType.GetMethods())
                {
                    var action = CreateMethodDescriptor(method);

                    var methodSignature = method.GetMethodSignature();

                    var methodDescriptor = new MethodDescriptor(methodSignature, method, action, interfaceType, controllerType);
                    contractMethods.TryAdd($"{methodDescriptor.MethodSignature}", methodDescriptor);
                    OnMethodRegistered?.Invoke(methodDescriptor);
                }
            }
            
        }

        public bool GetMethodDescriptor(IOperationRequest request, out IMethodDescriptor? value)
        {
            if (request == null)
            {
                value = null;
                return false;
            }

            if (ControllerMethods.TryGetValue(request.ContractName, out Dictionary<string, IMethodDescriptor>? descriptors) 
                && descriptors.TryGetValue(request.OperationName, out IMethodDescriptor? descriptor))
            {
                value = descriptor;
                return true;
            }

            value = null;
            return false;
        }

        private MethodDelegate CreateMethodDescriptor(MethodInfo method)
        {
            var instanceParam = Expression.Parameter(typeof(object), "instance");
            var argsParam = Expression.Parameter(typeof(object[]), "args");

            var parameters = method.GetParameters();
            var arguments = new Expression[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                var index = Expression.Constant(i);
                var paramType = parameters[i].ParameterType;

                var argAccess = Expression.ArrayIndex(argsParam, index);
                var argCast = Expression.Convert(argAccess, paramType);

                arguments[i] = argCast;
            }

            var instanceCast = method.IsStatic ? null : Expression.Convert(instanceParam, method.DeclaringType!);
            var call = Expression.Call(instanceCast, method, arguments);

            Expression body = method.ReturnType == typeof(void)
                ? Expression.Block(call, Expression.Constant(null, typeof(object)))
                : Expression.Convert(call, typeof(object));

            var lambda = Expression.Lambda<MethodDelegate>(body, instanceParam, argsParam);
            return lambda.Compile();
        }
    }
}
