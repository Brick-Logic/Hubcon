using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Attributes;
using Hubcon.Shared.Abstractions.Interfaces;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Hubcon.Client.Abstractions.Interfaces
{
    public interface ITransportClient
    {
        public Task<HubconResponse<T>> SendAsync<T>(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken = default);
        public Task<HubconResponse<bool>> CallAsync(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken = default);
        public IAsyncEnumerable<JsonElement> GetStream(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken = default);
        public IAsyncEnumerable<JsonElement> GetSubscription(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken = default);
        public Task<HubconResponse<T>> Ingest<T>(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken = default);

        public void Build(TransportContext context);
        public bool IsBuilt();
    }
}

namespace Hubcon
{
    public class TransportContext
    {
        public TransportContext(
            IServiceProvider proxyServiceProvider,
            IInterceptorManager interceptorManager,
            IClientOptions clientOptions,
            IContractOptions contractOptions,
            Uri uri,
            Func<IAuthenticationManager>? authenticationManagerFactory,
            string baseUrl,
            string originalUrl,
            bool useSecureConnection,
            IDynamicConverter converter,
            string webSocketUrl,
            string httpUrl)
        {
            ProxyServiceProvider = proxyServiceProvider;
            InterceptorManager = interceptorManager;
            ClientOptions = clientOptions;
            ContractOptions = contractOptions;
            Uri = uri;
            AuthenticationManagerFactory = authenticationManagerFactory;
            BaseUrl = baseUrl;
            OriginalUrl = originalUrl;
            UseSecureConnection = useSecureConnection;
            Converter = converter;
            WebSocketUrl = webSocketUrl;
            HttpUrl = httpUrl;
        }

        public IServiceProvider ProxyServiceProvider { get; }
        public IInterceptorManager InterceptorManager { get; }
        public IClientOptions ClientOptions { get; }
        public IContractOptions ContractOptions { get; }
        public Uri Uri { get; }
        public Func<IAuthenticationManager>? AuthenticationManagerFactory { get; }
        public string BaseUrl { get; }
        public string OriginalUrl { get; }
        public bool UseSecureConnection { get; }
        public IDynamicConverter Converter { get; }
        public string WebSocketUrl { get; }
        public string HttpUrl { get; }
    }

    public interface ITransportClient<TAttribute> : ITransportClient
        where TAttribute : HubconTransportAttribute
    {
    }

    public abstract class TransportClient<TAttribute> : ITransportClient<TAttribute> where TAttribute : HubconTransportAttribute
    {
        private TransportContext? _context;
        protected TransportContext Context { get => _context!; }

        public abstract Task<HubconResponse<T>> SendAsync<T>(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken = default);
        public abstract Task<HubconResponse<bool>> CallAsync(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken = default);
        public abstract IAsyncEnumerable<JsonElement> GetStream(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken = default);
        public abstract IAsyncEnumerable<JsonElement> GetSubscription(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken = default);
        public abstract Task<HubconResponse<T>> Ingest<T>(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken = default);

        void ITransportClient.Build(TransportContext context)
        {
            _context ??= context;
            Build(context);
        }

        bool ITransportClient.IsBuilt()
        {
            return _context != null;
        }

        protected abstract void Build(TransportContext context);
    }
}
