using Castle.DynamicProxy;
using Hubcon.Interceptors;
using System.Data;

namespace Hubcon
{
    /// <summary>
    /// Allows a server to control a client given the client's interface type and a ServerHub type.
    /// This class can be injected.
    /// </summary>
    /// <typeparam name="THub"></typeparam>
    /// <typeparam name="TIClientController"></typeparam>
    internal class ClientConnector<THub, TIClientController> : IClientManager<THub, TIClientController>
        where THub : ServerHub
        where TIClientController : IClientController?
    {
#pragma warning disable S2743 // Static fields should not be used in generic types
        private static readonly ProxyGenerator ProxyGen = new();
#pragma warning restore S2743 // Static fields should not be used in generic types

        protected Func<ClientControllerConnectorInterceptor<THub>> interceptorFactory;
        protected Dictionary<string, TIClientController>? clients = [];

        public ClientConnector(ClientControllerConnectorInterceptor<THub> interceptor)
        {
            interceptorFactory = () => interceptor;
        }

        protected TIClientController BuildInstance(string instanceId)
        {
            var interceptor = interceptorFactory.Invoke();
            interceptor.WithClientId(instanceId);

            return (TIClientController)ProxyGen.CreateInterfaceProxyWithTarget(
                typeof(TIClientController),
                (TIClientController)DynamicImplementationCreator.CreateImplementation(typeof(TIClientController)),
                interceptor
            );
        }

        public TIClientController GetClient(string instanceId)
        {
            if (clients!.TryGetValue(instanceId, out TIClientController? value))
                return value;

            var client = BuildInstance(instanceId);
            clients.TryAdd(instanceId, client);
            return client;
        }

        public IEnumerable<string> GetAllClients()
        {
            return ServerHub.GetClients(typeof(THub)).Select(x => x.Id);
        }

        public void RemoveInstance(string instanceId)
        {
            clients?.Remove(instanceId, out _);
        }     
    }
}
