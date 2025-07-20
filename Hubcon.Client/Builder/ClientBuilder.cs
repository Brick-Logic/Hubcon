using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Client.Core.Configurations;
using Hubcon.Client.Core.Subscriptions;
using Hubcon.Client.Interceptors;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Standard.Interceptor;
using Hubcon.Shared.Abstractions.Standard.Interfaces;
using Hubcon.Shared.Core.Tools;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Reflection;

namespace Hubcon.Client.Builder
{
    internal sealed class ClientBuilder(IProxyRegistry proxyRegistry) : IClientBuilder, IClientOptions
    {
        public Uri? BaseUri { get; set; }
        public List<Type> Contracts { get; set; } = new();
        public Type? AuthenticationManagerType { get; set; }
        public string? HttpPrefix { get; set; }
        public string? WebsocketPrefix { get; set; }
        public Action<ClientWebSocketOptions>? WebSocketOptions { get; set; }
        public Action<HttpClient>? HttpClientOptions { get; set; }
        public bool UseSecureConnection { get; set; } = true;
        public TimeSpan? WebsocketPingInterval { get; set; }

        private ConcurrentDictionary<Type, Type> _subTypesCache { get; } = new();
        private ConcurrentDictionary<Type, IEnumerable<PropertyInfo>> _propTypesCache { get; } = new();
        private ConcurrentDictionary<Type, IContractOptions> _contractOptions { get; } = new();
        private Dictionary<Type, object> _clients { get; } = new();


        public T GetOrCreateClient<T>(IServiceProvider services) where T : IControllerContract
        {
            return (T)GetOrCreateClient(typeof(T), services);
        }

        public object GetOrCreateClient(Type contractType, IServiceProvider services)
        {
            if (_clients.ContainsKey(contractType) && _clients.TryGetValue(contractType, out object? client))
                return client!;

            if (!Contracts.Any(x => x == contractType))
                return default!;

            var proxyType = proxyRegistry.TryGetProxy(contractType);

            var hubconClient = services.GetService<IHubconClient>();

            hubconClient?.Build(this, services, _contractOptions, UseSecureConnection);

            var newClient = (BaseContractProxy)services.GetRequiredService(proxyType);
            var proxyInterceptor = services.GetRequiredService<ClientProxyInterceptor>();

            proxyInterceptor.InjectClient(hubconClient!);
            newClient.BuildContractProxy(proxyInterceptor);

            var props = _propTypesCache.GetOrAdd(
                proxyType,
                x => x.GetProperties().Where(x => x.PropertyType.IsAssignableTo(typeof(ISubscription))));

            foreach (var subscriptionProp in props)
            {
                var value = subscriptionProp.GetValue(newClient, null);
                if (value == null)
                {
                    var genericType = _subTypesCache.GetOrAdd(
                        subscriptionProp.PropertyType.GenericTypeArguments[0], 
                        x => typeof(ClientSubscriptionHandler<>).MakeGenericType(x));

                    var subscriptionInstance = (ISubscription)services.GetRequiredService(genericType);
                    PropertyTools.AssignProperty(newClient, subscriptionProp, subscriptionInstance);
                    PropertyTools.AssignProperty(subscriptionInstance, nameof(ClientSubscriptionHandler<object>.Property), subscriptionProp);
                    PropertyTools.AssignProperty(subscriptionInstance, nameof(ClientSubscriptionHandler<object>.Client), hubconClient);
                    subscriptionInstance.Build();
                }
            }

            _clients.Add(contractType, newClient!);

            return newClient!;
        }

        public void UseAuthenticationManager<T>(IServiceCollection services) where T : class, IAuthenticationManager
        {
            if (AuthenticationManagerType != null)
                return;

            AuthenticationManagerType = typeof(T);

            HubconClientBuilder.Current.Services.AddSingleton<T>();
        }

        public void LoadContractProxy(Type contractType, IServiceCollection services)
        {
            HubconClientBuilder.Current.LoadContractProxy(contractType, services);
        }

        public void ConfigureContract<T>(Action<IContractConfigurator>? configure = null) where T : IControllerContract
        {
            if (configure == null)
                return;

            if(!_contractOptions.TryGetValue(typeof(T), out _))
            {
                var options = new ContractOptions<T>();
                configure(options);
                _contractOptions.TryAdd(typeof(T), options);
            }
        }
    }
}