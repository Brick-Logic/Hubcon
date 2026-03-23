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
    /// <summary>
    /// Represents the comprehensive execution context for a client-side operation within the Hubcon framework.
    /// Acts as a central repository for metadata, configuration, and state required to execute an RPC or streaming call.
    /// </summary>
    public sealed class ClientOperationContext : IClientOperationContext
    {
        /// <summary>
        /// Gets a value indicating whether the method signature has been hashed for transport.
        /// </summary>
        public bool SignatureIsHashed { get; }

        /// <summary>
        /// Gets the <see cref="MemberInfo"/> representing the method or property being invoked on the contract.
        /// </summary>
        public MemberInfo Member { get; }

        /// <summary>
        /// Gets the global configuration options for the Hubcon client.
        /// </summary>
        public IClientOptions ClientOptions { get; }

        /// <summary>
        /// Gets the configuration options specific to the service contract.
        /// </summary>
        public IContractOptions ContractOptions { get; }

        /// <summary>
        /// Gets the configuration options specific to the individual operation being executed.
        /// </summary>
        public IOperationOptions OperationOptions { get; }

        /// <summary>
        /// Gets the <see cref="Type"/> of the contract interface.
        /// </summary>
        public Type ContractType { get; }

        /// <summary>
        /// Gets the unique string signature of the method being called.
        /// </summary>
        public string MethodSignature { get; }

        /// <summary>
        /// Gets the final <see cref="Uri"/> used for the request.
        /// </summary>
        public Uri Uri { get; }

        /// <summary>
        /// Gets the factory delegate used to resolve the <see cref="IAuthenticationManager"/> for this context.
        /// </summary>
        public Func<IAuthenticationManager>? AuthenticationManagerFactory { get; }

        /// <summary>
        /// Gets the transport client responsible for sending the request (e.g., WebSocket or HTTP).
        /// </summary>
        public ITransportClient Transport { get; }

        /// <summary>
        /// Gets a value indicating whether the operation supports remote cancellation via token.
        /// </summary>
        public bool RemoteCancellationIsAllowed { get; }

        /// <summary>
        /// Gets the scoped <see cref="IServiceProvider"/>, falling back to the root provider if no active scope exists.
        /// </summary>
        public IServiceProvider ScopedServiceProvider => HubconContext.Current?.Services ?? RootServiceProvider;

        /// <summary>
        /// Gets the root <see cref="IServiceProvider"/> for the application.
        /// </summary>
        public IServiceProvider RootServiceProvider { get; }

        /// <summary>
        /// Gets the current <see cref="IInvocationContext"/> from the <see cref="HubconContext"/>.
        /// </summary>
        public IInvocationContext CallContext => HubconContext.Current;

        /// <summary>
        /// Gets the list of attributes applied to the member being invoked.
        /// </summary>
        public List<Attribute> Attributes { get; }

        /// <summary>
        /// Gets a value indicating whether the operation requires an authenticated session.
        /// </summary>
        public bool RequiresAuthentication { get; }

        /// <summary>
        /// Gets the defined <see cref="HttpMethod"/> for the request, if applicable.
        /// </summary>
        public HttpMethod? HttpMethodDefined { get; }

        /// <summary>
        /// Gets the HTTP method attribute metadata associated with the operation.
        /// </summary>
        public HttpMethodDataAttribute? HttpMethodAttribute { get; }

        /// <summary>
        /// Gets a value indicating whether the current member is a method.
        /// </summary>
        public bool IsMethod { get; }

        /// <summary>
        /// Gets the base URL of the remote service.
        /// </summary>
        public string BaseUrl { get; }

        /// <summary>
        /// Gets the original URL string before any protocol transformations.
        /// </summary>
        public string OriginalUrl { get; }

        /// <summary>
        /// Gets a value indicating whether a secure connection (HTTPS/WSS) is being used.
        /// </summary>
        public bool UseSecureConnection { get; }

        /// <summary>
        /// Gets the <see cref="IDynamicConverter"/> used for object serialization and transformation.
        /// </summary>
        public IDynamicConverter Converter { get; }

        /// <summary>
        /// Gets the formatted WebSocket URL for this operation.
        /// </summary>
        public string WebSocketUrl { get; }

        /// <summary>
        /// Gets the formatted HTTP URL for this operation.
        /// </summary>
        public string HttpUrl { get; }

        /// <summary>
        /// Gets a value indicating whether the client expects a standard Hubcon envelope in the response.
        /// </summary>
        public bool ExpectsHubconResponse { get; }

        /// <summary>
        /// Gets a dictionary of dynamic header providers that resolve values using the service provider.
        /// </summary>
        public IReadOnlyDictionary<string, Func<IServiceProvider, string>> HeaderProviders { get; }

        /// <summary>
        /// Gets a dictionary of static HTTP headers to be included in the request.
        /// </summary>
        public IReadOnlyDictionary<string, string> StaticHeaders { get; }

        /// <summary>
        /// Gets a set of headers requested specifically for this operation.
        /// </summary>
        public HashSet<string> RequestedHeaders { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ClientOperationContext"/> class.
        /// </summary>
        /// <param name="member">The <see cref="MemberInfo"/> of the contract member.</param>
        /// <param name="interceptorManager">The manager for hooks and interceptors.</param>
        /// <param name="serviceProvider">The application service provider.</param>
        /// <param name="clientOptions">Global client configuration.</param>
        /// <param name="contractOptions">Contract-specific configuration.</param>
        /// <param name="contractType">The interface type of the contract.</param>
        /// <param name="transports">A dictionary of available transport clients indexed by type.</param>
        public ClientOperationContext(
            MemberInfo member,
            InterceptorManager interceptorManager,
            IServiceProvider serviceProvider,
            IClientOptions clientOptions,
            IContractOptions contractOptions,
            Type contractType,
            Dictionary<Type, ITransportClient> transports)
        {
            IsMethod = member is MethodInfo;
            Member = member;
            UseSecureConnection = clientOptions.UseSecureConnection;
            Converter = serviceProvider.GetRequiredService<IDynamicConverter>();
            var env = Environment.GetEnvironmentVariable("HUBCON_OPNAME_DEBUG_ENABLED");
            ClientOptions = clientOptions;
            ContractOptions = contractOptions;
            ContractType = contractType;
            RootServiceProvider = serviceProvider;

            // Attributes
            Attributes = new List<Attribute>();
            Attributes.AddRange(Member.GetCustomAttributes());

            if (clientOptions.AuthenticationManagerType != null && clientOptions.AuthenticationManagerFactory != null)
            {
                AuthenticationManagerFactory = () => clientOptions.AuthenticationManagerFactory.GetValue<IAuthenticationManager>(serviceProvider);
            }

            if (member is MethodInfo method)
            {
                SignatureIsHashed = !bool.TryParse(env, out var parsed) ? true : !parsed;
                MethodSignature = method.GetMethodSignature(SignatureIsHashed);
                Member = method;
                OperationOptions = contractOptions.GetOperationOptions(MethodSignature, member);
                var httpMethod = TryFindHttpMethod(method);
                var parameters = method.GetParameters();
                HttpMethodAttribute = httpMethod != null ? httpMethod : ((parameters.Length - parameters.Count(x => x.ParameterType == typeof(CancellationToken)) > 0) ? new HttpPostAttribute() : new HttpGetAttribute());
                HttpMethodDefined = HttpMethodAttribute.HttpMethod;


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
            //else if (member is PropertyInfo propertyInfo)
            //{
            //    SignatureIsHashed = false;
            //    MethodSignature = propertyInfo.Name;
            //    Member = propertyInfo;
            //    OperationOptions = contractOptions.GetOperationOptions(MethodSignature, member);
            //    var httpMethod = TryFindHttpMethod(Member);
            //    HttpMethodAttribute = httpMethod;

            //    HttpGetAttribute? verb = Member.GetCustomAttribute<HttpGetAttribute>();
            //    HttpMethodDefined = HttpMethodAttribute != null ? HttpMethodAttribute.HttpMethod : HttpMethod.Get;
            //}
            else
            {
                throw new NotSupportedException();
            }

            // Transport
            var transportAttributeType = OperationOptions.MemberInfo.GetCustomAttribute<HubconTransportAttribute>() 
                ?? OperationOptions?.TransportType 
                ?? ContractType.GetCustomAttribute<HubconTransportAttribute>() 
                ?? contractOptions.TransportType 
                ?? clientOptions.TransportType;

            var transportType = TransportTypeResolver.Resolve(transportAttributeType.GetType())!;
            Transport = (ITransportClient)serviceProvider.GetRequiredService(transportType);

            // Authentication
            this.RequiresAuthentication = OperationOptions?.AuthIsEnabled ?? ContractOptions.AuthIsEnabled ?? ClientOptions.AuthIsEnabled;

            RemoteCancellationIsAllowed = OperationOptions?.RemoteCancellationIsAllowed ?? contractOptions.RemoteCancellationIsAllowed ?? ClientOptions.RemoteCancellationIsAllowed;

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

            transports.TryAdd(transportAttributeType.GetType(), Transport);

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

            var headerProviders = new Dictionary<string, Func<IServiceProvider, string>>();
            foreach (var item in OperationOptions!.HeaderProviders) headerProviders.TryAdd(item.Key, item.Value);
            foreach (var item in contractOptions.HeaderProviders) headerProviders.TryAdd(item.Key, item.Value);
            foreach (var item in clientOptions.HeaderProviders) headerProviders.TryAdd(item.Key, item.Value);
            HeaderProviders = headerProviders;

            var staticHeaders = new Dictionary<string, string>();
            var requestedHeaders = new HashSet<string>();

            foreach (var item in OperationOptions.MemberInfo.GetCustomAttributes<HeaderAttribute>()) 
            {
                if (item.IsStatic)
                {
                    staticHeaders.TryAdd(item.Key, item.Value!);
                }

                requestedHeaders.Add(item.Key);
            }

            foreach (var item in contractOptions.ContractType.GetCustomAttributes<HeaderAttribute>())
            {
                if (item.IsStatic)
                {
                    staticHeaders.TryAdd(item.Key, item.Value!);
                }

                requestedHeaders.Add(item.Key);
            }

            RequestedHeaders = requestedHeaders;
            StaticHeaders = staticHeaders;
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

        /// <summary>
        /// Asynchronously acquires the necessary permits from the configured rate limiters before proceeding with the operation.
        /// Checks across global, HTTP-specific, and operation-specific rate buckets.
        /// </summary>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous acquisition operation.</returns>
        public async ValueTask AcquireRateLimiter()
        {
            await RateLimiterHelper.AcquireAsync(ClientOptions, ClientOptions.RateBucket, ClientOptions.HttpFireAndForgetRateBucket, OperationOptions.RateBucket);
        }

        /// <summary>
        /// Asynchronously triggers both hooks and interceptors for the specified <see cref="HookType"/>.
        /// Ensures an <see cref="InterceptorManager"/> is initialized in the current context if not already present.
        /// </summary>
        /// <param name="hookType">The category of hook to execute.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
        public async ValueTask CallHooksAndInterceptors(HookType hookType, CancellationToken cancellationToken = default)
        {
            var interceptorManager = InterceptorContext.Current;
            if (interceptorManager == null) InterceptorContext.UseContext(new InterceptorManager(ScopedServiceProvider, ClientOptions, ContractOptions, OperationOptions, CallContext));
            await InterceptorContext.Current.CallHooksAndInterceptors(hookType, cancellationToken);
        }

        /// <summary>
        /// Asynchronously triggers hooks for the specified <see cref="HookType"/>.
        /// Ensures an <see cref="InterceptorManager"/> is initialized in the current context if not already present.
        /// </summary>
        /// <param name="hookType">The category of hook to execute.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
        public async ValueTask CallHooks(HookType hookType, CancellationToken cancellationToken = default)
        {
            var interceptorManager = InterceptorContext.Current;
            if (interceptorManager == null) InterceptorContext.UseContext(new InterceptorManager(ScopedServiceProvider, ClientOptions, ContractOptions, OperationOptions, CallContext));
            await InterceptorContext.Current.CallHooks(hookType, cancellationToken);
        }

        /// <summary>
        /// Asynchronously triggers the validation hooks for the current operation.
        /// Ensures an <see cref="InterceptorManager"/> is initialized in the current context if not already present.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
        public async ValueTask CallValidationHooks(CancellationToken cancellationToken = default)
        {
            var interceptorManager = InterceptorContext.Current;
            if (interceptorManager == null) InterceptorContext.UseContext(new InterceptorManager(ScopedServiceProvider, ClientOptions, ContractOptions, OperationOptions, CallContext));
            await InterceptorContext.Current.CallValidationHooks(cancellationToken);
        }

        /// <summary>
        /// Sets the raw response result for the current wrapped execution context.
        /// </summary>
        /// <param name="result">The <see cref="IResponse"/> to assign.</param>
        /// <returns>A <see cref="ValueTask"/> representing the completion of the operation.</returns>
        public async ValueTask SetResponse(IResponse result)
        {
            WrappedContext.CurrentWrapped.SetResponse(result);
        }

        /// <summary>
        /// Sets a typed Hubcon response result for the current wrapped execution context.
        /// </summary>
        /// <typeparam name="T">The type of the data contained in the response.</typeparam>
        /// <param name="result">The <see cref="IHubconResponse{T}"/> to assign.</param>
        /// <returns>A <see cref="ValueTask"/> representing the completion of the operation.</returns>
        public async ValueTask SetResponse<T>(IHubconResponse<T> result)
        {
            WrappedContext.CurrentWrapped.SetResponse(result);
        }

        /// <summary>
        /// Processes and deserializes the raw transport response into the expected result type.
        /// Handles both standard Hubcon envelopes and raw data based on the <see cref="ExpectsHubconResponse"/> configuration.
        /// </summary>
        /// <typeparam name="T">The expected type of the response data.</typeparam>
        /// <param name="response">The raw response object received from the transport.</param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous handling operation.</returns>
        public async ValueTask HandleResponse<T>(object response)
        {
            if (ExpectsHubconResponse)
            {
                try
                {
                    if (Converter.DeserializeData<T>(response) is not IResponse result)
                    {
                        result = HubconResponse.InternalError<T>(null!, "Parsing error.", response);
                        return;
                    }

                    await SetResponse(result!);
                }
                catch (Exception ex)
                {
                    await SetResponse<T>(HubconResponse.Fail<T>(response.ToString(), ex, originalData: response));
                }
            }
            else
            {
                try
                {
                    IResponse result = Converter.DeserializeData<HubconResponse<T>>(response);

                    if (result == null)
                    {
                        await SetResponse<T>(HubconResponse.InternalError<T>(null!, "Parsing error.", response));
                        return;
                    }

                    await SetResponse(result!);
                }
                catch (Exception ex)
                {
                    await SetResponse<T>(HubconResponse.Fail<T>(response.ToString(), ex, originalData: response));
                }
            }
        }

        /// <summary>
        /// Resolves and aggregates all required HTTP headers for the operation, including static values and dynamic providers.
        /// </summary>
        /// <param name="serviceProvider">The <see cref="IServiceProvider"/> used to resolve dynamic header values.</param>
        /// <returns>A <see cref="ValueTask"/> containing a dictionary of the resolved header keys and values.</returns>
        public async ValueTask<Dictionary<string, string>> GetHeaders(IServiceProvider serviceProvider)
        {
            Dictionary<string, string> headers = new();

            foreach (var headerKey in RequestedHeaders)
            {
                if (HeaderProviders.TryGetValue(headerKey, out var headerGetter))
                {
                    headers.TryAdd(headerKey, headerGetter.Invoke(serviceProvider));
                }
                else if (StaticHeaders.TryGetValue(headerKey, out var headerValue))
                {
                    headers.TryAdd(headerKey, headerValue);
                }
            }

            return headers;
        }
    }
}