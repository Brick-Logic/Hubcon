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
        public TICommunicationContract GetClient<TICommunicationContract>() where TICommunicationContract : ICommunicationContract;
    }

    /// <summary>
    /// The ServerHubConnector allows a client to connect itself to a ServerHub and control its methods given its URL and
    /// the server's interface.
    /// </summary>
    /// <typeparam name="TIServerHubController"></typeparam>
    public class HubconServerConnector<TIBaseHubconController, TICommunicationHandler> : IServerConnector
        where TICommunicationHandler : ICommunicationHandler
        where TIBaseHubconController : IBaseHubconController<TICommunicationHandler>
    {
        private ICommunicationContract? _client;
        private readonly ServerConnectorInterceptor<TIBaseHubconController, TICommunicationHandler> Interceptor;
        private readonly ProxyRegistry proxyRegistry;

        public ICommunicationHandler Connection { get => Interceptor.CommunicationHandler; }

        public HubconServerConnector(ServerConnectorInterceptor<TIBaseHubconController, TICommunicationHandler> interceptor, ProxyRegistry proxyRegistry) : base()
        {
            Interceptor = interceptor;
            this.proxyRegistry = proxyRegistry;
        }

        public ICommunicationContract? GetCurrentClient() => _client;

        public TICommunicationContract GetClient<TICommunicationContract>() where TICommunicationContract : ICommunicationContract
        {
            if (_client != null)
                return (TICommunicationContract)_client;

            var proxyType = proxyRegistry.TryGetProxy<TICommunicationContract>();

            return (TICommunicationContract)InstanceCreator.TryCreateInstance(proxyType, Interceptor)!;

            var proxyGenerator = new ProxyGenerator();

            _client = (TICommunicationContract)proxyGenerator.CreateInterfaceProxyWithTarget(
                typeof(TICommunicationContract),
                (TICommunicationContract)DynamicImplementationCreator.CreateImplementation(typeof(TICommunicationContract)),
                Interceptor
            );

            return (TICommunicationContract)_client;
        }
    }
}
