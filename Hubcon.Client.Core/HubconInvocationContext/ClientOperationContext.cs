using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Client.Core.Exceptions;
using Hubcon.Client.Core.Helpers;
using Hubcon.Client.Core.Transports;
using Hubcon.Shared.Abstractions.Attributes;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Abstractions.Standard.Extensions;
using Hubcon.Shared.Core.Context;
using Hubcon.Shared.Core.Extensions;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Hubcon.Client.Core.HubconInvocationContext
{
    public sealed class ClientOperationContext : IClientOperationContext
    {
        public bool SignatureIsHashed { get; }
        public MemberInfo Member { get; }
        public IClientOptions ClientOptions { get; }
        public IContractOptions ContractOptions { get; }
        public IOperationOptions OperationOptions { get; }
        public Type ContractType { get; }
        public string MethodSignature { get; }
        public Uri Uri { get; }
        public Func<IAuthenticationManager>? AuthenticationManagerFactory { get; }
        public ITransportClient Transport { get; }
        public bool RemoteCancellationIsAllowed { get; }
        public IServiceProvider ScopeServiceProvider => HubconContext.Current?.Services ?? RootServiceProvider;
        public IServiceProvider RootServiceProvider { get; }
        public IInvocationContext CallContext => HubconContext.Current;
        public List<Attribute> Attributes { get; }
        public bool RequiresAuthentication { get; }
        public HttpMethod? HttpMethodDefined { get; }
        public HttpMethodDataAttribute? HttpMethodAttribute { get; }
        public bool IsMethod { get; }
        public string BaseUrl { get; }
        public string OriginalUrl { get; }
        public bool UseSecureConnection { get; }
        public IDynamicConverter Converter { get; }
        public string WebSocketUrl { get; }
        public string HttpUrl { get; }
        public bool ExpectsHubconResponse { get; }

        public ClientOperationContext(MemberInfo member, InterceptorManager interceptorManager, IServiceProvider serviceProvider, IClientOptions clientOptions, IContractOptions contractOptions, Type contractType)
        {
            IsMethod = member is MethodInfo;
            Member = member;
            UseSecureConnection = clientOptions.UseSecureConnection;
            Converter = serviceProvider.GetRequiredService<IDynamicConverter>();
            var env = Environment.GetEnvironmentVariable("HUBCON_OPNAME_DEBUG_ENABLED");
            ClientOptions = clientOptions;
            ContractOptions = contractOptions;
            ContractType = contractType;
            RemoteCancellationIsAllowed = OperationOptions?.RemoteCancellationIsAllowed ?? contractOptions.RemoteCancellationIsAllowed;
            RootServiceProvider = serviceProvider;

            // Attributes
            Attributes = new List<Attribute>();
            Attributes.AddRange(Member.GetCustomAttributes());

            // Authentication
            this.RequiresAuthentication = (OperationOptions?.AuthIsEnabled ?? true)
                    && (ContractOptions.AuthIsEnabled)
                    && ClientOptions.AuthIsEnabled;

            // Transport
            var transportAttributeType = ContractType.GetCustomAttributes().FirstOrDefault(x => x is HubconTransportAttribute) ?? OperationOptions?.TransportType ?? contractOptions.TransportType ?? clientOptions.TransportType;
            var transportType = TransportTypeResolver.Resolve(transportAttributeType.GetType())!;
            Transport = (ITransportClient)serviceProvider.GetRequiredService(transportType);

            if (clientOptions.AuthenticationManagerType != null && clientOptions.AuthenticationManagerFactory != null)
            {
                AuthenticationManagerFactory = () => clientOptions.AuthenticationManagerFactory.GetValue<IAuthenticationManager>(serviceProvider);
            }

            if (member is MethodInfo method)
            {
                SignatureIsHashed = !bool.TryParse(env, out var parsed) ? true : !parsed;
                MethodSignature = method.GetMethodSignature(SignatureIsHashed);
                OperationOptions = contractOptions.GetOperationOptions(MethodSignature, method);
                Member = method;

                var httpMethod = TryFindHttpMethod(method);
                HttpMethodAttribute = httpMethod;

                HttpGetAttribute? verb = method.GetCustomAttribute<HttpGetAttribute>();
                HttpMethodDefined = HttpMethodAttribute != null ? HttpMethodAttribute.HttpMethod : (method.GetParameters().Length > 0 ? HttpMethod.Post : HttpMethod.Get);


                // Http validation
                var get = method.HasCustomAttribute<HttpGetAttribute>();

                if (get && !method.AreParametersValid())
                {
                    throw new HubconGenericException($"Method '{method.ReflectedType}.{method.Name}' cannot be used with GET verb as it contains types that cannot be converted to query strings. Use primitive types or use [AsQuery] for 1 complex type instead.");
                }

                foreach (var parameter in method.GetParameters())
                {
                    var asQuery = parameter.IsDefined(typeof(AsQueryAttribute));

                    if (asQuery && !parameter.ParameterType.IsTypeAllowed())
                    {
                        throw new HubconGenericException($"Parameter '{parameter.Name}' from method '{method.ReflectedType}.{method.Name}' cannot be used as query verb as it contains complex or null types. Use primitive or enum types instead.");
                    }
                }

                ExpectsHubconResponse = method.ReturnType.IsGenericType
                    && method.ReturnType.GenericTypeArguments[0].IsGenericType
                    && method.ReturnType.GenericTypeArguments[0].GetGenericTypeDefinition() == typeof(HubconResponse<>);
            }
            else if (member is PropertyInfo propertyInfo)
            {
                SignatureIsHashed = false;
                MethodSignature = propertyInfo.Name;
                OperationOptions = contractOptions.GetOperationOptions(MethodSignature, propertyInfo);
                Member = propertyInfo;

                var httpMethod = TryFindHttpMethod(Member);
                HttpMethodAttribute = httpMethod;

                HttpGetAttribute? verb = Member.GetCustomAttribute<HttpGetAttribute>();
                HttpMethodDefined = HttpMethodAttribute != null ? HttpMethodAttribute.HttpMethod : HttpMethod.Get;
            }
            else
            {
                throw new NotSupportedException();
            }

                Uri = ClientOptions.BaseUri ?? throw new ArgumentNullException("Base uri can't be null.");
            string baseRestHttpUrl = string.Empty;

            if (string.IsNullOrWhiteSpace(clientOptions.BaseUri?.Host))
            {
                BaseUrl = $"{Uri!.OriginalString.TrimEnd('/')}/{ClientOptions.HttpPrefix ?? ""}".TrimEnd('/');
            }
            else
            {
                BaseUrl = $"{Uri!.Host}:{Uri.Port}/{ClientOptions.HttpPrefix ?? ""}".TrimEnd('/');
            }

            OriginalUrl = Uri.OriginalString;

            var webSocketUrl = $"{BaseUrl.TrimEnd('/')}{ClientOptions.WebsocketPrefix ?? "/ws"}";
            WebSocketUrl = UseSecureConnection ? $"wss://{webSocketUrl}" : $"ws://{webSocketUrl}";

            var httpUrl = $"{BaseUrl.TrimEnd('/')}{ClientOptions.HttpPrefix}";
            HttpUrl = UseSecureConnection ? $"https://{httpUrl}" : $"http://{httpUrl}";

            if (!Transport.IsBuilt())
            {
                var transportConfiguration = new TransportContext(
                    RootServiceProvider,
                    interceptorManager,
                    ClientOptions,
                    ContractOptions,
                    Uri,
                    AuthenticationManagerFactory,
                    BaseUrl,
                    OriginalUrl,
                    clientOptions.UseSecureConnection,
                    Converter,
                    WebSocketUrl,
                    HttpUrl);

                Transport.Build(transportConfiguration);
            }

            var operationConfigurator = OperationOptions as IOperationConfigurator;
            var operationLimiter = Member.GetCustomAttribute<RateLimitAttribute>();
            if (operationLimiter != null)
            {
                operationConfigurator?.ConfigureRateBucket(operationLimiter);
            }
            else
            {
                var limiter = contractType.GetCustomAttribute<RateLimitAttribute>();
                if (limiter != null)
                {
                    operationConfigurator?.ConfigureRateBucket(new RateLimitAttribute(limiter.Requests, limiter.MillisecondsToReplenish, limiter.RateTokenLimit, limiter.QueueLimit));
                }
            }
        }

        private HttpMethodDataAttribute? TryFindHttpMethod(MemberInfo member)
        {
            if (member.GetCustomAttribute<HttpGetAttribute>() != null) return member.GetCustomAttribute<HttpGetAttribute>();
            else if (member.GetCustomAttribute<HttpPostAttribute>() != null) return member.GetCustomAttribute<HttpPostAttribute>();
            else if (member.GetCustomAttribute<HttpPutAttribute>() != null) return member.GetCustomAttribute<HttpPutAttribute>();
            else if (member.GetCustomAttribute<HttpDeleteAttribute>() != null) return member.GetCustomAttribute<HttpDeleteAttribute>();
            else if (member.GetCustomAttribute<HttpPatchAttribute>() != null) return member.GetCustomAttribute<HttpPatchAttribute>();
            else if (member.GetCustomAttribute<HttpHeadAttribute>() != null) return member.GetCustomAttribute<HttpHeadAttribute>();
            else if (member.GetCustomAttribute<HttpOptionsAttribute>() != null) return member.GetCustomAttribute<HttpOptionsAttribute>();
            else return null;
        }

        public async Task AcquireRateLimiter()
        {
            await RateLimiterHelper.AcquireAsync(ClientOptions, ClientOptions.RateBucket, ClientOptions.HttpFireAndForgetRateBucket, OperationOptions.RateBucket);
        }

        public async Task CallHooksAndInterceptors(HookType hookType, CancellationToken cancellationToken = default)
        {
            var interceptorManager = InterceptorContext.Current;
            if (interceptorManager == null) InterceptorContext.UseContext(new InterceptorManager(ScopeServiceProvider, ClientOptions, ContractOptions, OperationOptions, CallContext));
            await InterceptorContext.Current.CallHooksAndInterceptors(hookType, cancellationToken);
        }

        public async Task CallHooks(HookType hookType, CancellationToken cancellationToken = default)
        {
            var interceptorManager = InterceptorContext.Current;
            if (interceptorManager == null) InterceptorContext.UseContext(new InterceptorManager(ScopeServiceProvider, ClientOptions, ContractOptions, OperationOptions, CallContext));
            await InterceptorContext.Current.CallHooks(hookType, cancellationToken);
        }

        public async Task CallValidationHooks(CancellationToken cancellationToken = default)
        {
            var interceptorManager = InterceptorContext.Current;
            if (interceptorManager == null) InterceptorContext.UseContext(new InterceptorManager(ScopeServiceProvider, ClientOptions, ContractOptions, OperationOptions, CallContext));
            await InterceptorContext.Current.CallValidationHooks(cancellationToken);
        }

        public async Task SetResponse(IResponse result)
        {
            WrappedContext.CurrentWrapped.SetResponse(result);
        }

        public async Task HandleResponse<T>(JsonElement response)
        {
            if (ExpectsHubconResponse)
            {
                IResponse result = (Converter.DeserializeJsonElement<T>(response) as IResponse)!;
                await SetResponse(result!);
            }
            else
            {
                IResponse result = Converter.DeserializeJsonElement<HubconResponse<T>>(response);
                await SetResponse(result!);
            }
        }
    }
}