using Castle.DynamicProxy;
using Hubcon.Core.Injectors.Attributes;
using Hubcon.Core.Interceptors;
using Hubcon.Core.Models.Interfaces;
using Hubcon.Core.Registries;
using Hubcon.Core.Tools;

namespace Hubcon.Core.Connectors
{
    public interface IServerConnector
    {
        public TICommunicationContract GetClient<TICommunicationContract>() where TICommunicationContract : IHubconControllerContract;
    }

    /// <summary>
    /// The ServerHubConnector allows a client to connect itself to a ServerHub and control its methods given its URL and
    /// the server's interface.
    /// </summary>
    /// <typeparam name="TIServerHubController"></typeparam>
    public class HubconServerConnector<TICommunicationHandler> : IServerConnector
        where TICommunicationHandler : ICommunicationHandler
    {
        private IHubconControllerContract? _client = null!;
        private readonly ServerConnectorInterceptor<TICommunicationHandler> Interceptor;
        private readonly ProxyRegistry proxyRegistry;

        public ICommunicationHandler Connection { get => Interceptor.CommunicationHandler; }

        public HubconServerConnector(ServerConnectorInterceptor<TICommunicationHandler> interceptor, ProxyRegistry proxyRegistry) : base()
        {
            Interceptor = interceptor;
            this.proxyRegistry = proxyRegistry;
        }

        public TICommunicationContract GetClient<TICommunicationContract>() where TICommunicationContract : IHubconControllerContract
        {
            if (_client != null)
                return (TICommunicationContract)_client;

            var proxyType = proxyRegistry.TryGetProxy<TICommunicationContract>();
            _client = (TICommunicationContract)InstanceCreator.TryCreateInstance(proxyType, Interceptor)!;
            return (TICommunicationContract)_client;
        }
    }
}
