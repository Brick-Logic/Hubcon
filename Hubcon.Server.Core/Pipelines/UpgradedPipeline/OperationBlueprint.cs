using Hubcon.Server.Abstractions.CustomAttributes;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Core.Extensions;
using Hubcon.Shared.Abstractions.Attributes;
using Hubcon.Shared.Core.Extensions;
using Hubcon.Shared.Core.Lazy;
using Hubcon.Shared.Core.Tools;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using Hubcon.Server.Core.EndpointManagement;
using Hubcon.Server.Core.Pipelines.ResultHandlers;
using Hubcon.Shared.Abstractions.Standard.Extensions;

namespace Hubcon.Server.Core.Pipelines.UpgradedPipeline
{
    [StructLayout(LayoutKind.Sequential)]
    internal sealed class OperationBlueprint : IOperationBlueprint
    {
        public IPipelineBuilder PipelineBuilder { get; }
        public Type? CallWrapperType { get; }
        public ObjectFactory ControllerFactory { get; }
        public CompiledSecurityPolicy SecurityPolicy { get; }
        public IEndpointInvoker Invoker { get; }
        public IParameterWrapper? ParameterWrapper { get; }
        public string OperationName { get; }
        public string ContractName { get; }
        public string SimpleContractName { get; }

        public OperationKind Kind { get; }

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
        public HashSet<string> PrecomputedPolicies { get; private set; }
        public IList<Attribute> Attributes { get; }
        public ConcurrentDictionary<Type, Attribute> ConfigurationAttributes { get; }
        public ConcurrentDictionary<Type, Attribute> TransportAttributes { get; }
        
        public IImmutableList<PropertyInfo> WrapperProperties { get; }
        
        public HttpMethod? HttpVerb { get; }

        public bool ReturnsHubconResponse { get; }

        public OperationBlueprint(
            string operationName,
            Type contractType,
            Type controllerType,
            MemberInfo interfaceMemberInfo,
            MemberInfo controllerMemberInfo,
            OperationKind kind,
            IPipelineBuilder pipelineBuilder,
            IInternalServerOptions options,
            HttpMethod? httpMethod = null)
        {
            ArgumentException.ThrowIfNullOrEmpty(operationName);
            ArgumentNullException.ThrowIfNull(contractType);
            ArgumentNullException.ThrowIfNull(controllerType);
            ArgumentNullException.ThrowIfNull(interfaceMemberInfo);

            OperationName = operationName;
            ContractType = contractType;
            ContractName = contractType.Name;
            SimpleContractName = NamingHelper.GetCleanName(contractType.Name);
            ControllerType = controllerType;
            ControllerName = controllerType.Name;
            MemberInfo = interfaceMemberInfo;
            ParameterTypes = [];
            Kind = kind;
            List<Attribute> endpointAttributes = [];
            HttpVerb = httpMethod;
            // ControllerFactory = ActivatorUtilities.CreateFactory(controllerType, Type.EmptyTypes);

            if (interfaceMemberInfo is MethodInfo methodInfo)
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
                
                HasReturnType = ReturnType != typeof(void) && ReturnType != typeof(Task);

                Attributes = ControllerType.GetMethod(
                    interfaceMemberInfo.Name,
                    methodInfo.GetParameters().Select(x => x.ParameterType).ToArray())!
                    .GetCustomAttributes()
                    .ToList();

               ContractType.GetMethod(
                    interfaceMemberInfo.Name,
                    methodInfo.GetParameters().Select(x => x.ParameterType).ToArray())!
                    .GetCustomAttributes()
                    .ToList()
                    .ForEach(x => Attributes.Add(x));         

                endpointAttributes = Attributes
                    .Where(x => x is AuthorizeAttribute or AllowAnonymousAttribute or AnonymousAttribute)
                    .ToList();
            }
            else
            {
                throw new NotSupportedException($"The type {interfaceMemberInfo.GetType()} is not supported as an operation type. Use MethodInfo instead.");
            }

            ReturnsHubconResponse = ReturnType.IsGenericType
                && (ReturnType.GetGenericTypeDefinition() == typeof(IHubconResponse<>) || ReturnType.GetGenericTypeDefinition() == typeof(HubconResponse<>));
            
            var classAttributes = controllerType
                .GetCustomAttributes()
                .Where(x => x is AuthorizeAttribute or AllowAnonymousAttribute or AnonymousAttribute)
                .ToList();

            List<AuthorizeAttribute> combinedAuthorize = new List<AuthorizeAttribute>();

            if (endpointAttributes.Any(a => a is AllowAnonymousAttribute or AnonymousAttribute) || classAttributes.Any(a => a is AllowAnonymousAttribute or AnonymousAttribute))
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
                .ToHashSet()!;

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

            if(TransportAttributes.Count == 0)
            {
                Attributes
                .Where(x => x is HubconTransportAttribute)
                .ToList()
                .ForEach(x => TransportAttributes.TryAdd(x.GetType(), x));
            }

            if (TransportAttributes.Count == 0)
            {
                ControllerType
                    .GetCustomAttributes()
                    .OfType<HubconTransportAttribute>()
                    .ToList()
                    .ForEach(x => TransportAttributes.TryAdd(x.GetType(), x));

                ContractType
                    .GetCustomAttributes()
                    .OfType<HubconTransportAttribute>()
                    .ToList()
                    .ForEach(x => TransportAttributes.TryAdd(x.GetType(), x));
            }

            if (TransportAttributes.Count == 0)
            {
                foreach(var transport in options.DefaultTransports)
                {
                    TransportAttributes.TryAdd(transport.Key, transport.Value);
                }
            }

            PipelineBuilder = pipelineBuilder;
            Invoker = EndpointManager.GetInvoker(ControllerType, ContractType, methodInfo) 
                      ?? throw new HubconGenericException($"Could not find an invoker for the '{methodInfo.Name}' endpoint in '{methodInfo.DeclaringType}' controller. This error could be caused by an error while executing the source generators.");

            
            ParameterWrapper = ParameterTypes.IsEmpty ? null : new ParameterWrapper(ControllerType, contractType, methodInfo);
            
            CallWrapperType = EndpointManager.GetWrapperType(ControllerType, contractType, methodInfo);
            
            var handlerTypes = controllerType.GetCustomAttributes()
                .Concat(controllerMemberInfo!.GetCustomAttributes())
                .OfType<IUseAuthAttribute>();

            SecurityPolicy = new CompiledSecurityPolicy(
                handlerTypes.ToList(), 
                PrecomputedRoles.ToArray(), 
                PrecomputedPolicies.ToArray(), 
                !RequiresAuthorization
            );
        }
    }
}