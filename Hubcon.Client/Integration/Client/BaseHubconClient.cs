using Castle.DynamicProxy;
using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Standard.Interfaces;

namespace Hubcon.Client.Integration.Client
{
    public class HubconClientProvider : IHubconClientProvider
    {
        private readonly IServiceProvider _serviceProvider;
        private IControllerContract? _client = null!;
        private readonly IContractInterceptor Interceptor;
        private readonly IProxyRegistry _proxyRegistry;
        public ICommunicationHandler Connection { get => Interceptor.CommunicationHandler; }

        public HubconClientProvider(
            IContractInterceptor interceptor,
            IProxyRegistry proxyRegistry,
            IServiceProvider serviceProvider) : base()
        {
            Interceptor = interceptor;
            _proxyRegistry = proxyRegistry;
            _serviceProvider = serviceProvider;
        }

        public TICommunicationContract GetClient<TICommunicationContract>() where TICommunicationContract : IControllerContract
        {
            if (_client != null)
                return (TICommunicationContract)_client;

            var proxyType = _proxyRegistry.TryGetProxy<TICommunicationContract>();
            _client = (TICommunicationContract)_serviceProvider.GetService(proxyType);

            return (TICommunicationContract)_client;
        }
    }
}