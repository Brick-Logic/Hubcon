using Hubcon.Server.Abstractions.Delegates;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Core.Helpers;
using Hubcon.Server.Core.Middlewares;
using Hubcon.Server.Core.Pipelines.UpgradedPipeline;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Standard.Extensions;
using Hubcon.Shared.Core.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using Hubcon;

namespace Hubcon.Server.Core.Routing.Registries
{
    /// <summary>
    /// The operation registry implementation.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class OperationRegistry : IOperationRegistry
    {
        private readonly IInternalServerOptions _serverOptions;

        /// <summary>
        /// An event called when an operation is registered.
        /// </summary>
        public event Action<IOperationBlueprint>? OnOperationRegistered;

        private ConcurrentDictionary<string, IOperationBlueprint> _availableOperations = new ConcurrentDictionary<string, IOperationBlueprint>();

        private ConcurrentDictionary<Type, bool> RegisteredControllers = new();
        private readonly bool useHashedNames;

        private FrozenDictionary<string, IOperationBlueprint>? _blueprintCache;

        /// <summary>
        /// Determines if the registry is built. Once built it cannot be modified.
        /// </summary>
        public bool IsBuilt => _blueprintCache != null;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public OperationRegistry(IInternalServerOptions serverOptions)
        {
            _serverOptions = serverOptions;
            var env = Environment.GetEnvironmentVariable("HUBCON_OPNAME_DEBUG_ENABLED");
            useHashedNames = !bool.TryParse(env, out var parsed) ? true : !parsed;
        }

        ///<inheritdoc/>
        public void RegisterOperations(Type controllerType, Action<IControllerOptions>? options, out List<Action<IServiceCollection>> servicesToInject)
        {
            if (_blueprintCache != null)
                throw new InvalidOperationException("El registro de operaciones ya fue construido, no puede agregar mas operaciones.");

            if (!typeof(IControllerContract).IsAssignableFrom(controllerType))
                throw new NotImplementedException($"El tipo {controllerType.FullName} no implementa la interfaz {nameof(IControllerContract)} o un tipo derivado.");
            
            servicesToInject = new List<Action<IServiceCollection>>();

            void Injector(IServiceCollection x) => x.RegisterFactoryScoped(controllerType);
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
                        throw new InvalidOperationException($"Method '{method.Name}': Returning IAsyncEnumerable<T> and using IAsyncEnumerable<T> parameters at the same time is not supported.");

                    var hasReturnType = returnType != typeof(void) && returnType != typeof(Task);

                    OperationKind kind = isStream ? OperationKind.Stream
                                        : isIngest ? OperationKind.Ingest
                                        : !hasReturnType ? OperationKind.CallMethod : OperationKind.InvokeMethod;

                    if (method.IsStatic)
                        continue;

                    var parameterTypes = method.GetParameters().Select(x => x.ParameterType).ToArray();
                    var controllerMethod = controllerType.GetMethod(method.Name, parameterTypes);

                    Throw.If(controllerMethod == null, (controllerType, method), static x 
                        => new ArgumentNullException($"Could not find method {x.method.Name} in {x.controllerType.Name}."));
                    
                    var methodSignature = method.GetMethodSignature(useHashedNames);

                    var verb = method.GetCustomAttribute<HttpGetAttribute>();

                    if (verb != null && !method.AreParametersValid())
                    {
                        throw new InvalidOperationException($"Operation '{method.Name}' cannot be used with GET verb as it contains complex types. Use primitive types or a DTO class with primitive types instead.");
                    }
                    
                    if (verb == null)
                    {
                        bool hasComplexParameter = parameters.Any(p => 
                            p.ParameterType != typeof(CancellationToken) &&
                            !p.ParameterType.IsValueType &&
                            p.ParameterType != typeof(string) &&
                            p.ParameterType != typeof(DateTime) &&
                            p.ParameterType != typeof(TimeSpan) &&
                            p.ParameterType != typeof(Guid)
                        );

                        verb = hasComplexParameter ? null : new HttpGetAttribute();
                    }

                    var httpVerb = verb != null ? HttpMethod.Get : HttpMethod.Post;
                    
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
                            middlewareOptions.AddMiddleware(middleware.MiddlewareType, (IRegisterer)middleware);

                        foreach (var middleware in classFilters)
                            middlewareOptions.AddMiddleware(middleware.MiddlewareType, (IRegisterer)middleware);
                    }
                    else
                    {
                        foreach (var middleware in classFilters)
                            middlewareOptions.AddMiddleware(middleware.MiddlewareType, (IRegisterer)middleware);

                        foreach (var middleware in methodAttributes)
                            middlewareOptions.AddMiddleware(middleware.MiddlewareType, (IRegisterer)middleware);
                    }

                    var descriptor = new OperationBlueprint(
                        methodSignature,
                        interfaceType,
                        controllerType,
                        method,
                        controllerMethod,
                        kind,
                        pipelineBuilder,
                        _serverOptions,
                        httpVerb
                    );

                    foreach (var item in descriptor.SecurityPolicy.Handlers)
                    {
                        servicesToInject.Add(x => ((IRegisterer)item).Register(x));
                    }

                    _availableOperations.GetOrAdd(GetOperationKey(interfaceType.Name, descriptor.OperationName), descriptor);
                 
                    OnOperationRegistered?.Invoke(descriptor);
                }

                RegisteredControllers.TryAdd(controllerType, true);
            }
        }

        private static string GetOperationKey(string transportKey, string contractName, string operationName)
        {
            return transportKey + "_" + NamingHelper.GetCleanName(contractName) + "_" + operationName;
        }

        private static string GetOperationKey(string contractName, string operationName)
        {
            return NamingHelper.GetCleanName(contractName) + "_" + operationName;
        }

        private static readonly HashSet<Type> SimpleTypes = new()
        {
            typeof(string), typeof(decimal), typeof(DateTime), typeof(DateTimeOffset),
            typeof(TimeSpan), typeof(Guid), typeof(Uri), typeof(byte[])
        };

        private static bool IsQuerySupported(Type type)
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

        ///<inheritdoc/>
        public void MapTransport<T>(WebApplication app, Action<IReadOnlyDictionary<string, IOperationBlueprint>, WebApplication>? endpointRegisterer = null) where T : HubconTransportAttribute, new()
        {
            var transport = HubconTransportAttribute.GetDefault<T>();
            var tempCache = Build(transport);
            endpointRegisterer?.Invoke(tempCache, app);         
        }

        ///<inheritdoc/>
        public bool TryGetOperationBlueprint(IOperationEndpoint request, HubconTransportAttribute transportAttribute, out IOperationBlueprint? value)
        {
            if (request == null)
            {
                value = null;
                return false;
            }

            return GetOperationBlueprint(request.ContractName, request.OperationName, transportAttribute, out value);
        }

        ///<inheritdoc/>
        public bool GetOperationBlueprint(string contractName, string operationName, HubconTransportAttribute transportAttribute, out IOperationBlueprint? value)
        {
            if (_blueprintCache != null)
            {
                return _blueprintCache.TryGetValue(GetOperationKey(transportAttribute.TransportKey, contractName, operationName), out value);
            }

            if (_availableOperations.TryGetValue(contractName, out var descriptor))
            {
                value = descriptor;
                return true;
            }

            value = null;
            return false;
        }

        private IReadOnlyDictionary<string, IOperationBlueprint> Build(HubconTransportAttribute transport)
        {
            var tempCache = _blueprintCache?.ToDictionary() ?? new Dictionary<string, IOperationBlueprint>();
            var tempOperations = _availableOperations.Where(x => x.Value.TransportAttributes.Any(x => x.Key == transport.GetType())).ToFrozenDictionary();
            
            if (!_serverOptions.TransportSettings.TryGetValue(transport, out var settings))
                settings = transport.DefaultTransportSettings;
                
            foreach (var operation in tempOperations)
            {
                switch (operation.Value.Kind)
                {
                    case OperationKind.CallMethod when !settings.CallOperationEnabled:
                    case OperationKind.InvokeMethod when !settings.InvokeOperationEnabled:
                    case OperationKind.Stream when !settings.StreamOperationEnabled:
                    case OperationKind.Ingest when !settings.IngestOperationEnabled:
                        continue;
                    default:
                        tempCache.TryAdd(transport.TransportKey + "_" + operation.Key, operation.Value);
                        break;
                }
            }

            _blueprintCache = tempCache.ToFrozenDictionary();
            return tempOperations;
        }

        private static Func<object?, object, CancellationToken, object?> BuildWrapperInvoker(MethodInfo method, Type wrapperType)
        {
            var targetExp = Expression.Parameter(typeof(object), "target");
            var wrapperExp = Expression.Parameter(typeof(object), "wrapper");
            // 1. Agregamos el parámetro del token a la expresión
            var tokenExp = Expression.Parameter(typeof(CancellationToken), "ct");

            var typedWrapper = Expression.Convert(wrapperExp, wrapperType);
            var methodParams = method.GetParameters();
            var paramExps = new Expression[methodParams.Length];

            for (int i = 0; i < methodParams.Length; i++)
            {
                var param = methodParams[i];

                // 2. Si el parámetro es CancellationToken, inyectamos el 'tokenExp' directamente
                if (param.ParameterType == typeof(CancellationToken))
                {
                    paramExps[i] = tokenExp;
                    continue;
                }

                try
                {
                    paramExps[i] = Expression.PropertyOrField(typedWrapper, param.Name!);
                }
                catch (ArgumentException)
                {
                    throw new InvalidOperationException($"El parámetro '{param.Name}' no se encontró en el wrapper {wrapperType.Name}");
                }
            }

            Expression? instanceExp = method.IsStatic
                ? null
                : Expression.Convert(targetExp, method.DeclaringType!);

            MethodCallExpression callExp = Expression.Call(instanceExp, method, paramExps);

            // 3. Compilamos la Lambda con el nuevo parámetro 'tokenExp'
            if (method.ReturnType == typeof(void))
            {
                var block = Expression.Block(callExp, Expression.Constant(null, typeof(object)));
                return Expression.Lambda<Func<object?, object, CancellationToken, object?>>(
                    block, targetExp, wrapperExp, tokenExp).Compile();
            }
            else
            {
                var castCallExp = Expression.Convert(callExp, typeof(object));
                return Expression.Lambda<Func<object?, object, CancellationToken, object?>>(
                    castCallExp, targetExp, wrapperExp, tokenExp).Compile();
            }
        }

        private static Action<IDictionary<string, object>, object, CancellationToken> BuildMapper(Type wrapperType)
        {
            var dictParam = Expression.Parameter(typeof(IDictionary<string, object>), "dict");
            var wrapperParam = Expression.Parameter(typeof(object), "wrapperObj");
            var tokenParam = Expression.Parameter(typeof(CancellationToken), "ct");

            var typedWrapper = Expression.Convert(wrapperParam, wrapperType);
            var assignments = new List<Expression>();

            foreach (var prop in wrapperType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.PropertyType == typeof(CancellationToken))
                {
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

        ///<inheritdoc/>
        public bool ControllerExists(Type controllerType)
        {
            return RegisteredControllers.ContainsKey(controllerType);
        }
    }
}