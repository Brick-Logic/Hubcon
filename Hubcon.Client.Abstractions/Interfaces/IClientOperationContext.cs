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
    public interface IClientOperationContext
    {
        public bool SignatureIsHashed { get; }
        public MemberInfo Member { get; }
        public bool IsMethod { get; }
        public IClientOptions ClientOptions { get; }
        public IContractOptions ContractOptions { get; }
        public IOperationOptions OperationOptions { get; }
        public Type ContractType { get; }
        public string MethodSignature { get; }
        public Uri Uri { get; }
        public Func<IAuthenticationManager>? AuthenticationManagerFactory { get; }
        public ITransportClient Transport { get; }
        public bool RemoteCancellationIsAllowed { get; }
        HttpMethod? HttpMethodDefined { get; }
        bool RequiresAuthentication { get; }
        List<Attribute> Attributes { get; }
        IServiceProvider ScopeServiceProvider { get; }
        IServiceProvider RootServiceProvider { get; }
        IInvocationContext CallContext { get; }
        HttpMethodDataAttribute? HttpMethodAttribute { get; }
        bool UseSecureConnection { get; }
        string BaseUrl { get; }
        IDynamicConverter Converter { get; }
        string OriginalUrl { get; }
        string WebSocketUrl { get; }
        string HttpUrl { get; }
        bool ExpectsHubconResponse { get; }
        HashSet<string> RequestedHeaders { get; }

        ValueTask AcquireRateLimiter();
        ValueTask CallHooks(HookType hookType, CancellationToken cancellationToken = default);
        ValueTask CallHooksAndInterceptors(HookType hookType, CancellationToken cancellationToken = default);
        ValueTask CallValidationHooks(CancellationToken cancellationToken = default);
        ValueTask<Dictionary<string, string>> GetHeaders(IServiceProvider serviceProvider);
        ValueTask HandleResponse<T>(object response);
        ValueTask SetResponse(IResponse result);
    }
}