using Castle.DynamicProxy;
using Hubcon.Core.Interceptors;
using Hubcon.Core.Interfaces.Communication;
using Hubcon.Core.Models.Interfaces;

namespace Hubcon.Core.Connectors
{
    /// <summary>
    /// The ServerHubConnector allows a client to connect itself to a ServerHub and control its methods given its URL and
    /// the server's interface.
    /// </summary>
    /// <typeparam name="TIServerHubController"></typeparam>
    public class HubconServerConnector<TICommunicationContract, TICommunicationHandler> : HubconClientBuilder<TICommunicationContract>, IConnector
        where TICommunicationContract : ICommunicationContract
        where TICommunicationHandler : ICommunicationHandler
    {
        private TICommunicationContract? _client;
        private readonly TICommunicationHandler _connectionHandler;

        public TICommunicationHandler Connection { get => _connectionHandler; }

        public HubconServerConnector(TICommunicationHandler handler) : base()
        {
            _connectionHandler = handler;
        }

        public TICommunicationContract? GetCurrentClient() => _client;

        public TICommunicationContract GetClient()
        {
            if (_client != null)
                return _client;

            var proxyGenerator = new ProxyGenerator();

            _client = (TICommunicationContract)proxyGenerator.CreateInterfaceProxyWithTarget(
                typeof(TICommunicationContract),
                (TICommunicationContract)DynamicImplementationCreator.CreateImplementation(typeof(TICommunicationContract)),
                new ServerConnectorInterceptor(Connection)
            );

            return _client;
        }
    }
}
