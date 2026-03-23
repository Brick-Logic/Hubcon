using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Attributes;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Hubcon
{
    /// <summary>
    /// Defines the execution context for a client-side operation. 
    /// Orchestrates the lifecycle of a request from pre-flight validation and rate limiting 
    /// to transport execution and response transformation.
    /// </summary>
    public interface IClientOperationContext
    {
        /// <summary>Gets a value indicating whether the method signature has been pre-hashed.</summary>
        bool SignatureIsHashed { get; }

        /// <summary>Gets the reflection metadata for the contract member being invoked.</summary>
        MemberInfo Member { get; }

        /// <summary>Gets a value indicating whether the current member is a method.</summary>
        bool IsMethod { get; }

        /// <summary>Gets the global client configuration options.</summary>
        IClientOptions ClientOptions { get; }

        /// <summary>Gets the configuration options for the specific service contract.</summary>
        IContractOptions ContractOptions { get; }

        /// <summary>Gets the configuration options for the specific operation.</summary>
        IOperationOptions OperationOptions { get; }

        /// <summary>Gets the interface type of the service contract.</summary>
        Type ContractType { get; }

        /// <summary>Gets the unique string identifier for the method being called.</summary>
        string MethodSignature { get; }

        /// <summary>Gets the target <see cref="Uri"/> for the operation.</summary>
        Uri Uri { get; }

        /// <summary>Gets the factory used to resolve the authentication manager for this call.</summary>
        Func<IAuthenticationManager>? AuthenticationManagerFactory { get; }

        /// <summary>Gets the transport client responsible for the physical communication.</summary>
        ITransportClient Transport { get; }

        /// <summary>Gets a value indicating whether the operation supports remote cancellation tokens.</summary>
        bool RemoteCancellationIsAllowed { get; }

        /// <summary>Gets the defined <see cref="HttpMethod"/> for the request, if applicable.</summary>
        HttpMethod? HttpMethodDefined { get; }

        /// <summary>Gets a value indicating whether this operation requires an authenticated session.</summary>
        bool RequiresAuthentication { get; }

        /// <summary>Gets the attributes applied to the contract member.</summary>
        List<Attribute> Attributes { get; }

        /// <summary>Gets the service provider for the current request scope.</summary>
        IServiceProvider ScopedServiceProvider { get; }

        /// <summary>Gets the root application service provider.</summary>
        IServiceProvider RootServiceProvider { get; }

        /// <summary>Gets the current invocation state and results.</summary>
        IInvocationContext CallContext { get; }

        /// <summary>Gets the specific HTTP metadata attribute for the operation.</summary>
        HttpMethodDataAttribute? HttpMethodAttribute { get; }

        /// <summary>Gets a value indicating whether SSL/TLS is enabled for this connection.</summary>
        bool UseSecureConnection { get; }

        /// <summary>Gets the base service URL.</summary>
        string BaseUrl { get; }

        /// <summary>Gets the serialization engine used for this operation.</summary>
        IDynamicConverter Converter { get; }

        /// <summary>Gets the original URL before protocol-specific formatting.</summary>
        string OriginalUrl { get; }

        /// <summary>Gets the formatted WebSocket-specific URL.</summary>
        string WebSocketUrl { get; }

        /// <summary>Gets the formatted HTTP-specific URL.</summary>
        string HttpUrl { get; }

        /// <summary>Gets a value indicating whether the client expects a standard Hubcon response envelope.</summary>
        bool ExpectsHubconResponse { get; }

        /// <summary>Gets the set of header keys required for this operation.</summary>
        HashSet<string> RequestedHeaders { get; }

        /// <summary>
        /// Asynchronously waits for and acquires the necessary rate-limiting permits for the call.
        /// </summary>
        ValueTask AcquireRateLimiter();

        /// <summary>
        /// Triggers the registration-level hooks for the specified lifecycle stage.
        /// </summary>
        ValueTask CallHooks(HookType hookType, CancellationToken cancellationToken = default);

        /// <summary>
        /// Triggers both contract hooks and client-level interceptors for the specified lifecycle stage.
        /// </summary>
        ValueTask CallHooksAndInterceptors(HookType hookType, CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes validation logic defined for the operation before the request is sent.
        /// </summary>
        ValueTask CallValidationHooks(CancellationToken cancellationToken = default);

        /// <summary>
        /// Resolves the final dictionary of HTTP headers using the provided service provider.
        /// </summary>
        ValueTask<Dictionary<string, string>> GetHeaders(IServiceProvider serviceProvider);

        /// <summary>
        /// Processes the raw transport response, performing deserialization and error handling.
        /// </summary>
        ValueTask HandleResponse<T>(object response);

        /// <summary>
        /// Manually sets the final response result for the operation.
        /// </summary>
        ValueTask SetResponse(IResponse result);
    }
}