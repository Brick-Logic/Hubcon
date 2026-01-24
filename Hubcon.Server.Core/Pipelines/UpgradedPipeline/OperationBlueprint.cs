using Hubcon.Server.Abstractions.CustomAttributes;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Core.Extensions;
using Hubcon.Shared.Abstractions.Enums;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Core.Extensions;
using Hubcon.Shared.Core.Tools;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Reflection;

namespace Hubcon.Server.Core.Pipelines.UpgradedPipeline
{
    internal sealed class OperationBlueprint : IOperationBlueprint
    {
        public string OperationName { get; }
        public OperationKind Kind { get; }

        public string ContractName { get; }
        public string SimpleContractName { get; }
        public Type ContractType { get; }

        public string ControllerName { get; }
        public Type ControllerType { get; }

        public ConcurrentDictionary<string, Type> ParameterTypes { get; }
        public Type RawReturnType { get; }
        public Type ReturnType { get; }
        public bool HasReturnType { get; }

        public MemberInfo? OperationInfo { get; }

        public bool RequiresAuthorization { get; }
        public IEnumerable<AuthorizeAttribute> AuthorizationAttributes { get; }
        public HashSet<string> PrecomputedRoles { get; private set; }
        public string?[] PrecomputedPolicies { get; private set; }
        public IEnumerable<Attribute> Attributes { get; }
        public ConcurrentDictionary<Type, Attribute> ConfigurationAttributes { get; }
        public Func<object?, object, object?>? InvokeDelegate { get; }
        public IPipelineBuilder PipelineBuilder { get; }

        public string? HttpRoute { get; }
        public string? HttpEndpointGroupName { get; }

        public Type? CallWrapperType { get; }
        public Action<IDictionary<string, object>, object, CancellationToken>? WrapperMapper { get; }
        public HttpMethod? HttpVerb { get; }
        public IReadOnlyList<(PropertyInfo PropInfo, Action<object, object?> FastSetter)> SubscriptionProperties { get; }
        public bool HasSubscriptions { get; }
        public ObjectFactory ControllerFactory { get; }

        public OperationBlueprint(
            string operationName,
            Type contractType,
            Type controllerType,
            MemberInfo memberInfo,
            OperationKind kind,
            IPipelineBuilder pipelineBuilder,
            IInternalServerOptions options,
            HttpMethod? httpMethod = null,
            Type? callWrapperType = null,
            Action<IDictionary<string, object>, object, CancellationToken>? wrapperMapper = null,
            Func<object?, object, object?>? invokeDelegate = null)
        {
            ArgumentException.ThrowIfNullOrEmpty(operationName);
            ArgumentNullException.ThrowIfNull(contractType);
            ArgumentNullException.ThrowIfNull(controllerType);
            ArgumentNullException.ThrowIfNull(memberInfo);

            OperationName = operationName;
            ContractType = contractType;
            ContractName = contractType.Name;
            SimpleContractName = NamingHelper.GetCleanName(contractType.Name);
            ControllerType = controllerType;
            ControllerName = controllerType.Name;
            OperationInfo = memberInfo;
            ParameterTypes = [];
            Kind = kind;
            CallWrapperType = callWrapperType;
            WrapperMapper = wrapperMapper;
            List<Attribute> endpointAttributes = [];
            HttpVerb = httpMethod;
            ControllerFactory = ActivatorUtilities.CreateFactory(controllerType, Type.EmptyTypes);

            SubscriptionProperties = controllerType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(ISubscription<>))
                .Select(prop => (prop, prop.CreateFastSetter()))
                .ToList();

            HasSubscriptions = SubscriptionProperties.Count > 0;

            if (memberInfo is MethodInfo methodInfo)
            {
                foreach (var parameter in methodInfo.GetParameters())
                {
                    if (parameter.ParameterType == typeof(CancellationToken))
                        continue;

                    ParameterTypes.TryAdd(parameter.Name!, parameter.ParameterType);
                }

                RawReturnType = methodInfo.ReturnType;

                ReturnType = methodInfo.ReturnType.IsGenericType &&
                       methodInfo.ReturnType.GetGenericTypeDefinition() == typeof(Task<>)
                       ? methodInfo.ReturnType.GetGenericArguments()[0]
                       : methodInfo.ReturnType;

                var combinedRoute = methodInfo.GetRoute(options.MethodOverloadingIsEnabled);
                HttpRoute = options.HttpPathPrefix + combinedRoute.Endpoint;
                HttpEndpointGroupName = combinedRoute.EndpointGroup;

                HasReturnType = ReturnType != typeof(void) && ReturnType != typeof(Task);

                Attributes = ControllerType.GetMethod(
                    memberInfo.Name,
                    methodInfo.GetParameters().Select(x => x.ParameterType).ToArray())!
                    .GetCustomAttributes();

                endpointAttributes = Attributes
                    .Where(x => x is AuthorizeAttribute || x is AllowAnonymousAttribute)
                    .ToList();
            }
            else if (memberInfo is PropertyInfo propertyInfo)
            {
                ReturnType = propertyInfo.PropertyType;
                RawReturnType = propertyInfo.PropertyType;
                HasReturnType = true;

                Kind = OperationKind.Subscription;

                Attributes = ControllerType.GetMethod(propertyInfo.Name)?.GetCustomAttributes() ?? new List<Attribute>();

                endpointAttributes = Attributes
                    .Where(x => x is SubscriptionAuthorizeAttribute || x is AllowAnonymousAttribute)
                    .ToList();
            }
            else
            {
                throw new NotSupportedException($"The type {memberInfo.GetType()} is not supported as an operation type. Use PropertyInfo o MethodInfo instead.");
            }

            var classAttributes = controllerType
                .GetCustomAttributes()
                .Where(x => x is AuthorizeAttribute || x is AllowAnonymousAttribute)
                .ToList();

            List<AuthorizeAttribute> combinedAuthorize = new List<AuthorizeAttribute>();

            // Si el método tiene AllowAnonymous, ignora todo Authorize
            if (endpointAttributes.Any(a => a is AllowAnonymousAttribute) || classAttributes.Any(a => a is AllowAnonymousAttribute))
            {
                RequiresAuthorization = false;
            }
            else
            {
                // Tomar todos los Authorize del método + clase
                combinedAuthorize.AddRange(endpointAttributes.OfType<AuthorizeAttribute>());
                combinedAuthorize.AddRange(classAttributes.OfType<AuthorizeAttribute>());

                RequiresAuthorization = combinedAuthorize.Count > 0;
            }

            AuthorizationAttributes = combinedAuthorize;

            PrecomputedRoles = AuthorizationAttributes
                    .Where(a => !string.IsNullOrWhiteSpace(a.Roles))
                    .SelectMany(a => a.Roles?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [])
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

            PrecomputedPolicies = AuthorizationAttributes
                .Where(a => !string.IsNullOrWhiteSpace(a.Policy))
                .Select(a => a.Policy)
                .ToArray();

            ConfigurationAttributes = new();

            Attributes.Where(x =>
            {
                if (Kind == OperationKind.Subscription)
                {
                    return x is SubscriptionSettingsAttribute;
                }
                else if (Kind == OperationKind.Stream)
                {
                    return x is StreamingSettingsAttribute;
                }
                else if (Kind == OperationKind.Ingest)
                {
                    return x is IngestSettingsAttribute;
                }
                else if (Kind == OperationKind.CallMethod || Kind == OperationKind.InvokeMethod)
                {
                    return x is MethodSettingsAttribute;
                }
                else
                    return false;
            })
            .ToList()
            .ForEach(x =>
            {
                if (Kind == OperationKind.Subscription && x is SubscriptionSettingsAttribute subSettings)
                    ConfigurationAttributes.TryAdd(typeof(SubscriptionSettingsAttribute), subSettings);

                else if (Kind == OperationKind.Stream && x is StreamingSettingsAttribute streamSettings)
                    ConfigurationAttributes.TryAdd(typeof(StreamingSettingsAttribute), streamSettings);

                else if (Kind == OperationKind.Ingest && x is IngestSettingsAttribute ingestSettings)
                    ConfigurationAttributes.TryAdd(typeof(IngestSettingsAttribute), ingestSettings);

                else if ((Kind == OperationKind.CallMethod || Kind == OperationKind.InvokeMethod) && x is MethodSettingsAttribute methodSettings)
                    ConfigurationAttributes.TryAdd(typeof(MethodSettingsAttribute), methodSettings);
            });

            PipelineBuilder = pipelineBuilder;
            InvokeDelegate = invokeDelegate;
        }
    }
}