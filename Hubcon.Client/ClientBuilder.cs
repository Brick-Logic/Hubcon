using Autofac;
using Castle.DynamicProxy;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;
using Hubcon.Core.Abstractions.Interfaces;
using Hubcon.Core.Abstractions.Standard.Interfaces;
using Hubcon.Core.Middlewares.MessageHandlers;

namespace Hubcon.Client
{
    public class ClientBuilder(IProxyRegistry proxyRegistry) : IClientBuilder
    {
        public Uri? BaseUri { get; set; }
        public string? HttpEndpoint { get; set; }
        public string? WebsocketEndpoint { get; set; }
        public List<Type> Contracts { get; set; } = new();
        public Type? AuthenticationManagerType { get; set; }


        public bool UseSecureConnection { get; set; } = true;
        private bool IsBuilt { get; set; }
        private Dictionary<Type, object> _clients { get; } = new();
        private GraphQLHttpClient? _graphqlHttpClient { get; set; }
        private IAuthenticationManager? _authManager { get; set; }
        
        private void Build(ILifetimeScope lifetimeScope)
        {
            if(IsBuilt) return;

            var baseHttpUrl = $"{BaseUri!.AbsoluteUri}/{HttpEndpoint ?? "graphql"}";
            var baseWebsocketUrl = $"{BaseUri!.AbsoluteUri}/{WebsocketEndpoint ?? "graphql"}";

            var httpUrl = UseSecureConnection ? $"https://{baseHttpUrl}" : $"http://{baseHttpUrl}";
            var websocketUrl = UseSecureConnection ? $"wss://{baseWebsocketUrl}" : $"ws://{baseWebsocketUrl}";

            if(AuthenticationManagerType is not null)
                _authManager = (IAuthenticationManager?)lifetimeScope.ResolveOptional(AuthenticationManagerType!);

            var options = new GraphQLHttpClientOptions
            {
                EndPoint = new Uri(httpUrl),
                WebSocketEndPoint = new Uri(websocketUrl),
                WebSocketProtocol = "graphql-transport-ws",
                HttpMessageHandler = new HttpClientMessageHandler(_authManager)
            };

            _graphqlHttpClient = new GraphQLHttpClient(options, new SystemTextJsonSerializer());

            IsBuilt = true;
        }

        public T GetOrCreateClient<T>(ILifetimeScope lifetimeScope) where T : IControllerContract
        {
            return (T)GetOrCreateClient(typeof(T), lifetimeScope);
        }

        public object GetOrCreateClient(Type contractType, ILifetimeScope lifetimeScope)
        {
            if (!IsBuilt) Build(lifetimeScope);

            if (_clients.ContainsKey(contractType) && _clients.TryGetValue(contractType, out object? client))
                return client!;

            if (!Contracts.Any(x => x == contractType))
                return default!;

            var proxyType = proxyRegistry.TryGetProxy(contractType);

            var hubconClient = lifetimeScope.Resolve<IHubconClient>(new[]
            {
                new TypedParameter(typeof(GraphQLHttpClient), _graphqlHttpClient),
                new TypedParameter(typeof(IAuthenticationManager), _authManager)
            });

            var commHandler = lifetimeScope.Resolve<ICommunicationHandler>(new[]
            {
                new TypedParameter(typeof(IHubconClient), hubconClient)
            });

            var interceptor = lifetimeScope.Resolve<IContractInterceptor>(new[]
            {
                new TypedParameter(typeof(ICommunicationHandler), commHandler)
            });

            var newClient = lifetimeScope.Resolve(proxyType, new[]
            {
                new TypedParameter(typeof(AsyncInterceptorBase), interceptor)
            });

            _clients.Add(contractType, newClient);

            return newClient;
        }

        public void LoadContractProxy(Type contractType)
        {
            HubconClientBuilder.Current.LoadContractProxy(contractType);
        }
    }
}