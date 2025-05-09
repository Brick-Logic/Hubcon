using Hubcon.Core.Abstractions.Interfaces;
using Hubcon.Core.Abstractions.Standard.Interfaces;
using Hubcon.Core.Interceptors;
using Hubcon.Core.Tools;

namespace Hubcon.Core.Connectors
{
    /// <summary>
    /// Allows a server to control a client given the client's interface type and a ServerHub type.
    /// This class can be injected.
    /// </summary>
    /// <typeparam name="TICommunicationHandler"></typeparam>
    /// <typeparam name="TICommunicationContract"></typeparam>
    public class HubconClientConnector<TICommunicationContract> : IClientAccessor<TICommunicationContract>
        where TICommunicationContract : IControllerContract
    {
        protected Dictionary<string, TICommunicationContract>? clients = new();
        private readonly IProxyRegistry proxyRegistry;

        protected ClientControllerConnectorInterceptor Interceptor { get; set; }

        public HubconClientConnector(ClientControllerConnectorInterceptor interceptor, IProxyRegistry proxyRegistry)
        {
            Interceptor = interceptor;
            this.proxyRegistry = proxyRegistry;
        }

        protected TICommunicationContract BuildInstance(string instanceId)
        {
            var communicationHandler = (IServerCommunicationHandler)Interceptor.CommunicationHandler;
            communicationHandler.WithClientId(instanceId);

            var proxyType = proxyRegistry.TryGetProxy<TICommunicationContract>();
            return (TICommunicationContract)InstanceCreator.TryCreateInstance(proxyType, Interceptor)!;
        }

        public TICommunicationContract GetOrCreateClient(string instanceId)
        {
            if (clients!.TryGetValue(instanceId, out var value))
                return value;

            var client = BuildInstance(instanceId);
            clients.TryAdd(instanceId, client);
            return client;
        }

        public List<string> GetAllClients()
        {
            var handler = (IServerCommunicationHandler)Interceptor.CommunicationHandler;
            return handler.GetAllClients().Select(x => x.Id).ToList();
        }

        public void RemoveClient(string instanceId)
        {
            clients?.Remove(instanceId, out _);
        }

        public TCommunicationContract GetClient<TCommunicationContract>(string instanceId) where TCommunicationContract : IControllerContract
        {
            return (TCommunicationContract)(IControllerContract)GetOrCreateClient(instanceId)!;
        }
    }
}
