using Hubcon.Core.Extensions;
using Hubcon.Core.Models;
using Hubcon.Core.Models.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Core.MethodHandling
{
    public class MethodInvokerProvider
    {
        public event Action<HubconMethodInvoker>? OnMethodRegistered;

        internal Dictionary<string, Dictionary<string, HubconMethodInvoker>> ControllerMethods = new();

        public void RegisterMethods(Type controllerType)
        {
            if (!typeof(IHubconControllerContract).IsAssignableFrom(controllerType))
                throw new NotImplementedException($"El tipo {controllerType.FullName} no implementa la interfaz {nameof(IHubconControllerContract)} o un tipo derivado.");

            var interfaces = controllerType.GetInterfaces().Where(x => typeof(IHubconControllerContract).IsAssignableFrom(x));

            foreach (var interfaceType in interfaces)
            {
                if (interfaceType.GetMethods().Length == 0)
                    continue;

                if (!ControllerMethods.TryGetValue(interfaceType.Name, out Dictionary<string, HubconMethodInvoker>? contractMethods))
                    contractMethods = ControllerMethods[interfaceType.Name] = new();

                if (contractMethods.Count > 0)
                    continue;

                foreach (var method in interfaceType.GetMethods())
                {
                    var action = CreateMethodInvoker(method);

                    var methodSignature = method.GetMethodSignature();

                    var methodInvokerInfo = new HubconMethodInvoker(methodSignature, method, action, interfaceType, controllerType);
                    contractMethods.TryAdd($"{methodInvokerInfo.MethodSignature}", methodInvokerInfo);
                    OnMethodRegistered?.Invoke(methodInvokerInfo);
                }
            }
            
        }

        public bool GetMethodInvoker(MethodInvokeRequest request, out HubconMethodInvoker? value)
        {
            if (request == null)
            {
                value = null;
                return false;
            }

            if (ControllerMethods.TryGetValue(request.ContractName, out Dictionary<string, HubconMethodInvoker>? methods) 
                && methods.TryGetValue(request.MethodName, out HubconMethodInvoker? methodInvoker))
            {
                value = methodInvoker;
                return true;
            }

            value = null;
            return false;
        }

        private MethodInvokerDelegate CreateMethodInvoker(MethodInfo method)
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

            var lambda = Expression.Lambda<MethodInvokerDelegate>(body, instanceParam, argsParam);
            return lambda.Compile();
        }
    }
}
