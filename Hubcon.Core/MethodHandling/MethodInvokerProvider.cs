using Hubcon.Core.Extensions;
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
        internal Dictionary<Type, Dictionary<string, HubconMethodInvoker>> ControllerMethods = new();

        public void RegisterMethods(Type type, Action<HubconMethodInvoker>? forEachMethodAction = null)
        {
            if (!typeof(IBaseHubconController).IsAssignableFrom(type))
                throw new NotImplementedException($"El tipo {type.FullName} no implementa la interfaz {nameof(IBaseHubconController)} o un tipo derivado.");

            if (!ControllerMethods.TryGetValue(type, out Dictionary<string, HubconMethodInvoker>? methods))
                methods = ControllerMethods[type] = new();

            if (methods.Count == 0)
            {
                var interfaces = type.GetInterfaces().Where(x => typeof(ICommunicationContract).IsAssignableFrom(x));

                foreach (var item in interfaces)
                {
                    if (item.GetMethods().Length == 0)
                        continue;

                    foreach (var method in item.GetMethods())
                    {
                        var action = CreateMethodInvoker(method);

                        var methodSignature = method.GetMethodSignature();

                        var methodInvokerInfo = new HubconMethodInvoker(methodSignature, method, action);
                        forEachMethodAction?.Invoke(methodInvokerInfo);

                        methods.TryAdd($"{methodInvokerInfo.MethodSignature}", methodInvokerInfo);
                    }
                }
            }
        }

        public bool GetMethodInvoker(string methodName, Type type, out HubconMethodInvoker? value)
        {
            if (ControllerMethods.TryGetValue(type, out Dictionary<string, HubconMethodInvoker>? methods))
            {
                if (methods.TryGetValue(methodName, out HubconMethodInvoker? methodInvoker))
                {
                    value = methodInvoker;
                    return true;
                }
            }
            value = null;
            return false;
        }

        public MethodInvokerDelegate CreateMethodInvoker(MethodInfo method)
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
