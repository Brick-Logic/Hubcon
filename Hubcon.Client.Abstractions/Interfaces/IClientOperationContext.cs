using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Attributes;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Core.Context;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;
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
        IServiceProvider ServiceProvider { get; }
        IServiceProvider RootServiceProvider { get; }
        CallContext CallContext { get; }
        HttpMethodDataAttribute? HttpMethodAttribute { get; }
        bool UseSecureConnection { get; }
        string BaseUrl { get; }
        IDynamicConverter Converter { get; }
        string OriginalUrl { get; }
        string WebSocketUrl { get; }
        string HttpUrl { get; }

        Task AcquireRateLimiter();
        public Task CallHooks(HookType hookType);
        Task CallInterceptor(InterceptorType interceptorType);
        public Task CallValidationHooks();
    }
}