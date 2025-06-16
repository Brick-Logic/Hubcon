using Autofac;
using Hubcon.Server.Abstractions.Delegates;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Core.Extensions;
using Hubcon.Server.Core.Middlewares;
using Hubcon.Server.Core.Pipelines.UpgradedPipeline;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Standard.Interfaces;
using Hubcon.Shared.Core.Extensions;
using System.Linq.Expressions;
using System.Reflection;

namespace Hubcon.Server.Core.Routing.Registries
{
    public class OperationRegistry : IOperationRegistry
    {
        public event Action<IOperationBlueprint>? OnOperationRegistered;

        private Dictionary<string, Dictionary<string, IOperationBlueprint>> AvailableOperations = new();

        public void RegisterOperations(Type controllerType, Action<IMiddlewareOptions>? options, out List<Action<ContainerBuilder>> servicesToInject)
        {
            if (!typeof(IControllerContract).IsAssignableFrom(controllerType))
                throw new NotImplementedException($"El tipo {controllerType.FullName} no implementa la interfaz {nameof(IControllerContract)} o un tipo derivado.");

            servicesToInject = new List<Action<ContainerBuilder>>();

            void Injector(ContainerBuilder x) => x.RegisterWithInjector(x => x.RegisterType(controllerType).AsScoped().IfNotRegistered(controllerType));
            servicesToInject.Add(Injector);

            var interfaces = controllerType.GetInterfaces().Where(x => typeof(IControllerContract).IsAssignableFrom(x));

            foreach (var interfaceType in interfaces)
            {
                var methods = interfaceType.GetMethods();

                if (methods.Length == 0)
                    continue;

                if (!AvailableOperations.TryGetValue(interfaceType.Name, out Dictionary<string, IOperationBlueprint>? contractMethods))
                    contractMethods = AvailableOperations[interfaceType.Name] = new();

                if (contractMethods.Count > 0)
                    continue;

                foreach (var method in methods)
                {

                    var methodSignature = method.GetMethodSignature();
                    Func<object?, object[], object?> action = BuildInvoker(method);

                    var pipelineBuilder = new PipelineBuilder();
                    var middlewareOptions = new MiddlewareOptions(pipelineBuilder, servicesToInject);

                    options?.Invoke(middlewareOptions);

                    var descriptor = new OperationBlueprint(methodSignature, interfaceType, controllerType, method, pipelineBuilder, action!);

                    contractMethods.TryAdd($"{descriptor.OperationName}", descriptor);
                    OnOperationRegistered?.Invoke(descriptor);
                }

                var subscriptions = interfaceType
                    .GetProperties()
                    .Where(x => x.PropertyType.IsAssignableTo(typeof(ISubscription)));

                foreach (var propertyInfo in subscriptions)
                {
                    var pipelineBuilder = new PipelineBuilder();
                    var middlewareOptions = new MiddlewareOptions(pipelineBuilder, servicesToInject);

                    options?.Invoke(middlewareOptions);

                    var descriptor = new OperationBlueprint(propertyInfo.Name, interfaceType, controllerType, propertyInfo, pipelineBuilder);

                    contractMethods.TryAdd($"{descriptor.OperationName}", descriptor);
                    OnOperationRegistered?.Invoke(descriptor);
                }
            }
        }

        public bool GetOperationBlueprint(IOperationRequest request, out IOperationBlueprint? value)
        {
            if (request == null)
            {
                value = null;
                return false;
            }

            return GetOperationBlueprint(request.ContractName, request.OperationName, out value);
        }

        public bool GetOperationBlueprint(string contractName, string operationName, out IOperationBlueprint? value)
        {
            if (string.IsNullOrEmpty(contractName) || string.IsNullOrEmpty(operationName))
            {
                value = null;
                return false;
            }

            if (AvailableOperations.TryGetValue(contractName, out Dictionary<string, IOperationBlueprint>? descriptors)
                && descriptors.TryGetValue(operationName, out IOperationBlueprint? descriptor))
            {
                value = descriptor;
                return true;
            }

            value = null;
            return false;
        }

        private Delegate CreateMethodDescriptor(MethodInfo method)
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

        public static Func<object?, object[], object?> BuildInvoker(MethodInfo method)
        {
            // Parámetros de la función: (object? target, object[] args)
            var targetExp = Expression.Parameter(typeof(object), "target");
            var argsExp = Expression.Parameter(typeof(object[]), "args");

            // Obtener los parámetros del método y convertir cada uno
            var methodParams = method.GetParameters();
            var paramExps = new Expression[methodParams.Length];

            for (int i = 0; i < methodParams.Length; i++)
            {
                // args[i]
                var argAccess = Expression.ArrayIndex(argsExp, Expression.Constant(i));

                // Convertir object a tipo esperado (puede ser value type o ref type)
                var argCast = Expression.Convert(argAccess, methodParams[i].ParameterType);

                paramExps[i] = argCast;
            }

            // Expresión para la instancia (o null para estático)
            Expression instanceExp;
            if (method.IsStatic)
            {
                instanceExp = null; // para métodos estáticos no hay instancia
            }
            else
            {
                // Convertir object target a tipo del método (declaring type)
                instanceExp = Expression.Convert(targetExp, method.DeclaringType!);
            }

            // Crear la llamada al método
            MethodCallExpression callExp = Expression.Call(instanceExp, method, paramExps);

            // Si el método devuelve void, debemos devolver null
            if (method.ReturnType == typeof(void))
            {
                // Crear un bloque con la llamada y return null
                var block = Expression.Block(callExp, Expression.Constant(null, typeof(object)));

                return Expression.Lambda<Func<object?, object[], object?>>(block, targetExp, argsExp).Compile();
            }
            else
            {
                // Si devuelve valor, convertirlo a object
                var castCallExp = Expression.Convert(callExp, typeof(object));

                return Expression.Lambda<Func<object?, object[], object?>>(castCallExp, targetExp, argsExp).Compile();
            }
        }
    }
}
