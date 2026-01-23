using Hubcon.Server.Abstractions.Delegates;
using Hubcon.Server.Abstractions.Enums;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Core.Helpers;
using Hubcon.Server.Core.Middlewares;
using Hubcon.Server.Core.Pipelines.UpgradedPipeline;
using Hubcon.Shared.Abstractions.Attributes;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Standard.Extensions;
using Hubcon.Shared.Abstractions.Standard.Interfaces;
using Hubcon.Shared.Core.Extensions;
using Hubcon.Shared.Core.Tools;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Collections.Frozen;
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
        private readonly bool useHashedNames;

        private FrozenDictionary<(string Contract, string Operation), IOperationBlueprint>? _blueprintCache;

        public bool IsBuilt => _blueprintCache != null;

        public OperationRegistry()
        {
            var env = Environment.GetEnvironmentVariable("HUBCON_OPNAME_DEBUG_ENABLED");
            useHashedNames = !bool.TryParse(env, out var parsed) ? true : !parsed;
        }

        public void RegisterOperations(Type controllerType, Action<IControllerOptions>? options, IInternalServerOptions serverOptions, out List<Action<IServiceCollection>> servicesToInject)
        {
            if (_blueprintCache != null)
                throw new InvalidOperationException("El registro de operaciones ya fue construido, no puede agregar mas operaciones.");

            if (!typeof(IControllerContract).IsAssignableFrom(controllerType))
                throw new NotImplementedException($"El tipo {controllerType.FullName} no implementa la interfaz {nameof(IControllerContract)} o un tipo derivado.");

            servicesToInject = new List<Action<IServiceCollection>>();

            void Injector(IServiceCollection x) => x.AddScoped(controllerType);
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

                var contractMethods = AvailableOperations.GetOrAdd(NamingHelper.GetCleanName(interfaceType.Name), _ => new ConcurrentDictionary<string, IOperationBlueprint>());

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

                    var hasReturnType = returnType != typeof(void) && returnType != typeof(Task);

                    OperationKind kind = isStream ? OperationKind.Stream
                                        : isIngest ? OperationKind.Ingest
                                        : !hasReturnType ? OperationKind.CallMethod : OperationKind.InvokeMethod;

                    if (method.IsStatic)
                        continue;

                    if (!serverOptions.WebSocketMethodsIsAllowed && (kind == OperationKind.CallMethod || kind == OperationKind.InvokeMethod))
                        continue;

                    if (!serverOptions.WebSocketIngestIsAllowed && kind == OperationKind.Ingest)
                        continue;

                    if (!serverOptions.WebSocketStreamIsAllowed && kind == OperationKind.Stream)
                        continue;

                    var parameterTypes = method.GetParameters().Select(x => x.ParameterType).ToArray();
                    var controllerMethod = controllerType.GetMethod(method.Name, parameterTypes)!;

                    var methodSignature = method.GetMethodSignature(useHashedNames);

                    var verb = method.GetCustomAttribute<GetMethodAttribute>();

                    if (verb != null && !method.AreParametersValid())
                    {
                        throw new InvalidOperationException($"Operation '{method.Name}' cannot be used with GET verb as it contains complex types. Use primitive types or a DTO class with primitive types instead.");
                    }

                    var httpVerb = verb != null
                        ? HttpMethod.Get
                        : (parameters.Length - parameters.Count(x => x.ParameterType == typeof(CancellationToken)) > 0 ? HttpMethod.Post : HttpMethod.Get);

                    var wrapperType = ParameterWrapHelper.CreateWrapperType(controllerMethod, x =>
                    {
                        if (httpVerb == HttpMethod.Get)
                            return !x.ParameterType.IsTypeAllowed();
                        else
                            return true;
                    });

                    var wrapperMapper = BuildMapper(wrapperType);
                    Func<object?, object, object?> action = BuildWrapperInvoker(method, wrapperType);

                    var pipelineBuilder = new PipelineBuilder();
                    var middlewareOptions = new ControllerOptions(pipelineBuilder, servicesToInject);

                    options?.Invoke(middlewareOptions);

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
                            middlewareOptions.AddMiddleware(middleware.MiddlewareType, middleware.Cycle);

                        foreach (var middleware in classFilters)
                            middlewareOptions.AddMiddleware(middleware.MiddlewareType, middleware.Cycle);
                    }
                    else
                    {
                        foreach (var middleware in classFilters)
                            middlewareOptions.AddMiddleware(middleware.MiddlewareType, middleware.Cycle);

                        foreach (var middleware in methodAttributes)
                            middlewareOptions.AddMiddleware(middleware.MiddlewareType, middleware.Cycle);
                    }

                    var descriptor = new OperationBlueprint(
                        methodSignature,
                        interfaceType,
                        controllerType,
                        method,
                        kind,
                        pipelineBuilder,
                        serverOptions,
                        httpVerb,
                        wrapperType,
                        wrapperMapper,
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

        private static readonly HashSet<Type> SimpleTypes = new()
        {
            typeof(string), typeof(decimal), typeof(DateTime), typeof(DateTimeOffset),
            typeof(TimeSpan), typeof(Guid), typeof(Uri), typeof(byte[])
        };

        public static bool IsQuerySupported(Type type)
        {
            // 1. Descartar explícitamente tipos de infraestructura conocidos
            if (type == typeof(CancellationToken) || typeof(IProgress<>).IsAssignableFrom(type))
                return false;

            var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

            // 2. Primitivos (int, bool, char, etc) y Enums
            if (underlyingType.IsPrimitive || underlyingType.IsEnum)
                return true;

            // 3. Tipos simples conocidos
            if (SimpleTypes.Contains(underlyingType))
                return true;

            // 4. Soporte para Arrays de tipos simples (opcional)
            if (type.IsArray && IsQuerySupported(type.GetElementType()!))
                return true;

            return false;
        }

        public void MapControllers(WebApplication app)
        {
            foreach (var operationskvp in AvailableOperations)
            {
                foreach (var operation in operationskvp.Value)
                {
                    if (operation.Value.Kind != OperationKind.CallMethod && operation.Value.Kind != OperationKind.InvokeMethod && operation.Value.Kind != OperationKind.Stream)
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
            if (_blueprintCache != null)
            {
                return _blueprintCache.TryGetValue((contractName, operationName), out value);
            }

            // Fallback por si la caché no se ha buildeado aún
            if (AvailableOperations.TryGetValue(contractName, out var descriptors)
                && descriptors.TryGetValue(operationName, out var descriptor))
            {
                value = descriptor;
                return true;
            }

            value = null;
            return false;
        }

        public void Build()
        {
            if (IsBuilt)
                return;

            var flatList = AvailableOperations
                .SelectMany(contract => contract.Value
                    .Select(op => new KeyValuePair<(string, string), IOperationBlueprint>(
                        (contract.Key, op.Key), // Llave por valor (Tupla)
                        op.Value)))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            _blueprintCache = flatList.ToFrozenDictionary();
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

        public static Func<object?, object, object?> BuildWrapperInvoker(MethodInfo method, Type wrapperType)
        {
            // Parámetros de la función: (object? target, object wrapper)
            var targetExp = Expression.Parameter(typeof(object), "target");
            var wrapperExp = Expression.Parameter(typeof(object), "wrapper");

            // Convertir el object wrapper al tipo específico generado por IL
            var typedWrapper = Expression.Convert(wrapperExp, wrapperType);

            var methodParams = method.GetParameters();
            var paramExps = new Expression[methodParams.Length];

            for (int i = 0; i < methodParams.Length; i++)
            {
                var param = methodParams[i];

                // No necesitás buscar PropertyInfo ni FieldInfo. 
                // PropertyOrField busca en el wrapperType (que está en typedWrapper) 
                // un miembro que se llame igual que el parámetro.
                try
                {
                    paramExps[i] = Expression.PropertyOrField(typedWrapper, param.Name!);
                }
                catch (ArgumentException)
                {
                    throw new InvalidOperationException($"El parámetro '{param.Name}' no se encontró como Propiedad ni como Field en el wrapper {wrapperType.Name}");
                }
            }

            // Expresión para la instancia
            Expression? instanceExp = method.IsStatic
                ? null
                : Expression.Convert(targetExp, method.DeclaringType!);

            // Crear la llamada al método: target.Method(wrapper.Prop1, wrapper.Prop2...)
            MethodCallExpression callExp = Expression.Call(instanceExp, method, paramExps);

            // Manejo de retorno (void vs object)
            if (method.ReturnType == typeof(void))
            {
                var block = Expression.Block(callExp, Expression.Constant(null, typeof(object)));
                return Expression.Lambda<Func<object?, object, object?>>(block, targetExp, wrapperExp).Compile();
            }
            else
            {
                var castCallExp = Expression.Convert(callExp, typeof(object));
                return Expression.Lambda<Func<object?, object, object?>>(castCallExp, targetExp, wrapperExp).Compile();
            }
        }

        public static Action<IDictionary<string, object>, object, CancellationToken> BuildMapper(Type wrapperType)
        {
            var dictParam = Expression.Parameter(typeof(IDictionary<string, object>), "dict");
            var wrapperParam = Expression.Parameter(typeof(object), "wrapperObj");
            var tokenParam = Expression.Parameter(typeof(CancellationToken), "ct"); // <--- Nuevo parámetro

            var typedWrapper = Expression.Convert(wrapperParam, wrapperType);
            var assignments = new List<Expression>();

            foreach (var prop in wrapperType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                // CASO ESPECIAL: Si la propiedad del Wrapper es un CancellationToken
                if (prop.PropertyType == typeof(CancellationToken))
                {
                    // Asignación directa desde el parámetro 'ct', saltándose el diccionario
                    assignments.Add(Expression.Assign(Expression.Property(typedWrapper, prop), tokenParam));
                    continue;
                }

                // --- Tu lógica actual para el resto de propiedades ---
                var keyExp = Expression.Constant(prop.Name);
                var getItem = Expression.Property(dictParam, "Item", keyExp);
                var castExp = Expression.Convert(getItem, prop.PropertyType);
                var bindExp = Expression.Assign(Expression.Property(typedWrapper, prop), castExp);

                var containsKey = Expression.Call(dictParam, typeof(IDictionary<string, object>).GetMethod("ContainsKey")!, keyExp);
                assignments.Add(Expression.IfThen(containsKey, bindExp));
            }

            var body = Expression.Block(assignments);
            return Expression.Lambda<Action<IDictionary<string, object>, object, CancellationToken>>(
                body, dictParam, wrapperParam, tokenParam).Compile();
        }

        public bool ControllerExists(Type controllerType)
        {
            return RegisteredControllers.ContainsKey(controllerType);
        }
    }
}
