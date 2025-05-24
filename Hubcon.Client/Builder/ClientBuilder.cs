using Castle.DynamicProxy;
using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Client.Integration.Subscriptions;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Standard.Interfaces;
using Hubcon.Shared.Core.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace Hubcon.Client.Builder
{
    public class ClientBuilder(IProxyRegistry proxyRegistry) : IClientBuilder
    {
        public Uri? BaseUri { get; set; }
        public List<Type> Contracts { get; set; } = new();
        public Type? AuthenticationManagerType { get; set; }
        public string? HttpEndpoint { get; set; }
        public string? WebsocketEndpoint { get; set; }

        public bool UseSecureConnection { get; set; } = true;
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

            hubconClient?.Build(BaseUri!, HttpEndpoint, WebsocketEndpoint, AuthenticationManagerType, services, UseSecureConnection);

            var newClient = (IClientProxy)services.GetRequiredService(proxyType);
            var interceptor = services.GetRequiredService<IContractInterceptor>() as object;
            newClient.UseInterceptor((AsyncInterceptorBase)interceptor);
           
            foreach(var subscriptionProp in newClient.GetType().GetProperties().Where(x => x.PropertyType.IsAssignableTo(typeof(ISubscription))))
            {
                var value = subscriptionProp.GetValue(newClient, null);
                if (value == null)
                {
                    var genericType = typeof(ClientSubscriptionHandler<>).MakeGenericType(subscriptionProp.PropertyType.GenericTypeArguments[0]);
                    var subscriptionInstance = (ISubscription)services.GetRequiredService(genericType);
                    PropertyTools.AssignProperty(newClient, subscriptionProp, subscriptionInstance);
                    PropertyTools.AssignProperty(subscriptionInstance, nameof(subscriptionInstance.Property), subscriptionProp);
                    subscriptionInstance.Build();
                }
            }

            _clients.Add(contractType, newClient!);

            return newClient!;
        }

        public void UseAuthenticationManager<T>(IServiceCollection services) where T : IAuthenticationManager
        {
            if (AuthenticationManagerType != null)
                return;

            AuthenticationManagerType = typeof(T);

            HubconClientBuilder.Current.Services.AddSingleton(AuthenticationManagerType);
        }

        public void LoadContractProxy(Type contractType, IServiceCollection services)
        {
            HubconClientBuilder.Current.LoadContractProxy(contractType, services);
        }
    }
}