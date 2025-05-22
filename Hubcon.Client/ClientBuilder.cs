using Autofac;
using Castle.DynamicProxy;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;
using Hubcon.Core.Abstractions.Interfaces;
using Hubcon.Core.Abstractions.Standard.Interfaces;
using Hubcon.Core.Authentication;
using Hubcon.Core.Extensions;
using Hubcon.Core.Middlewares.MessageHandlers;

namespace Hubcon.Client
{
    public class ClientBuilder(IProxyRegistry proxyRegistry) : IClientBuilder
    {
        public Uri? BaseUri { get; set; }
        public List<Type> Contracts { get; set; } = new();
        public Type? AuthenticationManagerType { get; set; }
        public string? HttpEndpoint { get; set; }
        public string? WebsocketEndpoint { get; set; }

        public bool UseSecureConnection { get; set; } = true;
        private bool IsBuilt { get; set; }
        private Dictionary<Type, object> _clients { get; } = new();
        private GraphQLHttpClient? _graphqlHttpClient { get; set; }
        private Func<IAuthenticationManager?>? _authManager { get; set; }
        
        public T GetOrCreateClient<T>(IComponentContext context) where T : IControllerContract
        {
            return (T)GetOrCreateClient(typeof(T), context);
        }

        public object GetOrCreateClient(Type contractType, IComponentContext context)
        {
            if (_clients.ContainsKey(contractType) && _clients.TryGetValue(contractType, out object? client))
                return client!;

            if (!Contracts.Any(x => x == contractType))
                return default!;

            var proxyType = proxyRegistry.TryGetProxy(contractType);

            var hubconClient = context.Resolve<IHubconClient>();

            hubconClient.Build(BaseUri!, HttpEndpoint, WebsocketEndpoint, AuthenticationManagerType, context, UseSecureConnection);

            var commHandler = context.Resolve<ICommunicationHandler>(new[]
            {
                new TypedParameter(typeof(IHubconClient), hubconClient)
            });

            var interceptor = context.Resolve<IContractInterceptor>(new[]
            {
                new TypedParameter(typeof(ICommunicationHandler), commHandler)
            });

            var newClient = context.Resolve(proxyType, new[]
            {
                new TypedParameter(typeof(AsyncInterceptorBase), interceptor)
            });

            _clients.Add(contractType, newClient);

            return newClient;
        }

        public void UseAuthenticationManager<T>() where T : IAuthenticationManager
        {
            if (AuthenticationManagerType != null)
                return;

            AuthenticationManagerType = typeof(T);
            HubconClientBuilder.Current.AddService(container 
                => container.RegisterWithInjector(x => x.RegisterType(AuthenticationManagerType).AsSingleton()));
        }

        public void LoadContractProxy(Type contractType)
        {
            HubconClientBuilder.Current.LoadContractProxy(contractType);
        }
    }
}