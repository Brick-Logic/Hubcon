using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Core.Extensions;
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

        public MemberInfo? MemberInfo { get; }

        public bool RequiresAuthorization { get; }
        public IEnumerable<AuthorizeAttribute> AuthorizationAttributes { get; }
        public HashSet<string> PrecomputedRoles { get; private set; }
        public string?[] PrecomputedPolicies { get; private set; }
        public IList<Attribute> Attributes { get; }
        public ConcurrentDictionary<Type, Attribute> ConfigurationAttributes { get; }
        public ConcurrentDictionary<Type, Attribute> TransportAttributes { get; }
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
        public bool ReturnsHubconResponse { get; }

        public OperationBlueprint(
            string operationName,
            Type contractType,
            Type controllerType,
            MemberInfo intefaceMemberInfo,
            MemberInfo? controllerMemberInfo,
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
            ArgumentNullException.ThrowIfNull(intefaceMemberInfo);

            OperationName = operationName;
            ContractType = contractType;
            ContractName = contractType.Name;
            SimpleContractName = NamingHelper.GetCleanName(contractType.Name);
            ControllerType = controllerType;
            ControllerName = controllerType.Name;
            MemberInfo = intefaceMemberInfo;
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

            if (intefaceMemberInfo is MethodInfo methodInfo)
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
                    intefaceMemberInfo.Name,
                    methodInfo.GetParameters().Select(x => x.ParameterType).ToArray())!
                    .GetCustomAttributes()
                    .ToList();

                var interfaceAttributes = ControllerType.GetMethod(
                    intefaceMemberInfo.Name,
                    methodInfo.GetParameters().Select(x => x.ParameterType).ToArray())!
                    .GetCustomAttributes()
                    .ToList();

                endpointAttributes = Attributes
                    .Where(x => x is AuthorizeAttribute || x is AllowAnonymousAttribute)
                    .ToList();
            }
            else if (intefaceMemberInfo is PropertyInfo propertyInfo)
            {
                ReturnType = propertyInfo.PropertyType;
                RawReturnType = propertyInfo.PropertyType;
                HasReturnType = true;

                Kind = OperationKind.Subscription;

                Attributes = ControllerType.GetMethod(propertyInfo.Name)?.GetCustomAttributes().ToList() ?? new List<Attribute>();

                endpointAttributes = Attributes
                    .Where(x => x is SubscriptionAuthorizeAttribute || x is AllowAnonymousAttribute)
                    .ToList();
            }
            else
            {
                throw new NotSupportedException($"The type {intefaceMemberInfo.GetType()} is not supported as an operation type. Use PropertyInfo o MethodInfo instead.");
            }

            ReturnsHubconResponse = ReturnType.IsGenericType
                && (ReturnType.GetGenericTypeDefinition() == typeof(IHubconResponse<>) || ReturnType.GetGenericTypeDefinition() == typeof(HubconResponse<>));

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

            Attributes
                .Where(x => x is IConfigurationAttribute)
                .ToList()
                .ForEach(x => ConfigurationAttributes.TryAdd(x.GetType(), x));

            TransportAttributes = new();

            Attributes
                .Where(x => x is HubconTransportAttribute)
                .ToList()
                .ForEach(x => TransportAttributes.TryAdd(x.GetType(), x));

            PipelineBuilder = pipelineBuilder;
            InvokeDelegate = invokeDelegate;
        }
    }
}