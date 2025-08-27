using Autofac;
using Hubcon.Server.Abstractions.Delegates;
using Hubcon.Server.Abstractions.Enums;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Core.Configuration;
using Hubcon.Server.Core.Extensions;
using Hubcon.Server.Core.Middlewares;
using Hubcon.Server.Core.Pipelines.UpgradedPipeline;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Standard.Extensions;
using Hubcon.Shared.Abstractions.Standard.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;

namespace Hubcon.Server.Core.Routing.Registries
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class OperationRegistry : IOperationRegistry
    {
        public event Action<IOperationBlueprint>? OnOperationRegistered;

        private ConcurrentDictionary<string, ConcurrentDictionary<string, IOperationBlueprint>> AvailableOperations = new();
        private ConcurrentDictionary<Type, bool> RegisteredControllers = new();

        public void RegisterOperations(Type controllerType, Action<IControllerOptions>? options, IInternalServerOptions serverOptions, out List<Action<ContainerBuilder>> servicesToInject)
        {
            if (!typeof(IControllerContract).IsAssignableFrom(controllerType))
                throw new NotImplementedException($"El tipo {controllerType.FullName} no implementa la interfaz {nameof(IControllerContract)} o un tipo derivado.");

            servicesToInject = new List<Action<ContainerBuilder>>();

            void Injector(ContainerBuilder x) => x.RegisterWithInjector(x => x.RegisterType(controllerType).AsTransient().IfNotRegistered(controllerType));
            servicesToInject.Add(Injector);

            var interfaces = controllerType.GetInterfaces().Where(x => typeof(IControllerContract).IsAssignableFrom(x));

            foreach (var interfaceType in interfaces)
            {
                var methods = interfaceType
                    .GetMethods()
                    .Where(x => !x.Name.StartsWith("get_") && !x.Name.StartsWith("set_"))
                    .ToArray();

                if (methods.Length == 0)
                    continue;

                var contractMethods = AvailableOperations.GetOrAdd(interfaceType.Name, _ => new ConcurrentDictionary<string, IOperationBlueprint>());

                var classFilters = controllerType.GetCustomAttributes()
                        .Where(x => x is UseMiddlewareAttribute)
                        .Select(x => (UseMiddlewareAttribute)x);

                var middlewareOrder = controllerType
                    .GetCustomAttributes()
                    .Where(x => x is UseContractMiddlewaresFirst || x is UseOperationMiddlewaresFirst)
                    .FirstOrDefault();

                foreach (var method in methods)
                {
                    var returnType = method.ReturnType;
                    var parameters = method.GetParameters();

                    var isStream = returnType.IsGenericType &&
                                   returnType.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>);

                    var isIngest = parameters.Any(p =>
                        p.ParameterType.IsGenericType &&
                        p.ParameterType.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>));

                    if (isStream && isIngest)
                        throw new InvalidOperationException($"Method '{method.Name}': Returning IAsyncEnumerable<T> and also using IAsyncEnumerable<T> parameters is not supported.");

                    OperationKind kind = isStream ? OperationKind.Stream :
                                         isIngest ? OperationKind.Ingest :
                                         OperationKind.Method;

                    if (!serverOptions.WebSocketMethodsIsAllowed && kind == OperationKind.Method)
                        continue;

                    if (!serverOptions.WebSocketIngestIsAllowed && kind == OperationKind.Ingest)
                        continue;

                    if (!serverOptions.WebSocketStreamIsAllowed && kind == OperationKind.Stream)
                        continue;

                    var methodSignature = method.GetMethodSignature();
                    Func<object?, object[], object?> action = BuildInvoker(method);

                    var pipelineBuilder = new PipelineBuilder();
                    var middlewareOptions = new ControllerOptions(pipelineBuilder, servicesToInject);

                    options?.Invoke(middlewareOptions);

                    var parameterTypes = method.GetParameters().Select(x => x.ParameterType).ToArray();
                    var controllerMethod = controllerType.GetMethod(method.Name, parameterTypes);

                    var methodAttributes = controllerMethod!.GetCustomAttributes()
                        .Where(x => x is UseMiddlewareAttribute)
                        .Select(x => (UseMiddlewareAttribute)x);

                    var middlewareOrderMethod = controllerMethod!.GetCustomAttributes()
                        .Where(x => x is UseContractMiddlewaresFirst || x is UseOperationMiddlewaresFirst)
                        .FirstOrDefault();

                    var orderToUse = middlewareOrderMethod ?? middlewareOrder ?? new UseOperationMiddlewaresFirst();

                    if (orderToUse is UseOperationMiddlewaresFirst)
                    {
                        foreach (var middleware in methodAttributes)
                            middlewareOptions.AddMiddleware(middleware.MiddlewareType);

                        foreach (var middleware in classFilters)
                            middlewareOptions.AddMiddleware(middleware.MiddlewareType);
                    }
                    else
                    {
                        foreach (var middleware in classFilters)
                            middlewareOptions.AddMiddleware(middleware.MiddlewareType);

                        foreach (var middleware in methodAttributes)
                            middlewareOptions.AddMiddleware(middleware.MiddlewareType);
                    }

                    var descriptor = new OperationBlueprint(
                    methodSignature,
                    interfaceType,
                    controllerType,
                    method,
                    kind,
                    pipelineBuilder,
                    serverOptions,
                    action!
                    );


                    // Usa TryAdd para evitar sobreescritura accidental
                    contractMethods.TryAdd(descriptor.OperationName, descriptor);
                    OnOperationRegistered?.Invoke(descriptor);
                }

                var subscriptions = interfaceType
                    .GetProperties()
                    .Where(x => x.PropertyType.IsAssignableTo(typeof(ISubscription)));

                foreach (var propertyInfo in subscriptions)
                {
                    if (!serverOptions.WebSocketSubscriptionIsAllowed)
                        continue;

                    var pipelineBuilder = new PipelineBuilder();
                    var middlewareOptions = new ControllerOptions(pipelineBuilder, servicesToInject);

                    options?.Invoke(middlewareOptions);

                    var descriptor = new OperationBlueprint(
                        propertyInfo.Name,
                        interfaceType,
                        controllerType,
                        propertyInfo,
                        OperationKind.Subscription,
                        pipelineBuilder,
                        serverOptions
                    );

                    contractMethods.TryAdd(descriptor.OperationName, descriptor);
                    OnOperationRegistered?.Invoke(descriptor);
                }

                RegisteredControllers.TryAdd(controllerType, true);
            }
        }

        public void MapControllers(WebApplication app)
        {
            foreach (var operationskvp in AvailableOperations)
            {
                foreach (var operation in operationskvp.Value)
                {
                    if (operation.Value.Kind == OperationKind.Stream)
                        continue;

                    if (operation.Value.Kind == OperationKind.Subscription)
                        continue;

                    if (operation.Value.OperationInfo is MethodInfo)
                    {
                        app.MapTypedEndpoint(operation.Value);
                    }
                }
            }
        }

        public bool GetOperationBlueprint(IOperationEndpoint request, out IOperationBlueprint? value)
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

            if (AvailableOperations.TryGetValue(contractName, out var descriptors)
                && descriptors.TryGetValue(operationName, out var descriptor))
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

        public bool ControllerExists(Type controllerType)
        {
            return RegisteredControllers.ContainsKey(controllerType);
        }
    }
}
