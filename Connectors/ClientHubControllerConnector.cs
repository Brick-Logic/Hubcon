using Castle.DynamicProxy;
using Hubcon.Connectors;
using Hubcon.Interceptors;
using Hubcon.Models.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace Hubcon
{
    public class ClientHubControllerConnector<TIHubController, THub> : HubconControllerConnector<TIHubController>, IConnector
        where TIHubController : IHubController
        where THub : Hub
    {
        protected static Dictionary<string, TIHubController> Instances { get; } = [];

        protected IHubContext<THub>? HubContext;
        protected Hub? Hub;

        public ClientHubControllerConnector(IHubContext<THub> hubContext) : base() => HubContext = hubContext;
        public ClientHubControllerConnector(THub hubContext) : base() => Hub = hubContext;

        public TIHubController GetInstance(string instanceId)
        {
            if (Instances.TryGetValue(instanceId, out TIHubController? value))
                return value;

            var proxyGenerator = new ProxyGenerator();

            ClientHubControllerConnectorInterceptor interceptor = Hub == null
                ? new ClientHubControllerConnectorInterceptor(HubContext!, instanceId)
                : new ClientHubControllerConnectorInterceptor(Hub, instanceId);

            var client = (TIHubController)proxyGenerator.CreateInterfaceProxyWithTarget(
                typeof(TIHubController),
                (TIHubController)DynamicImplementationCreator.CreateImplementation(typeof(TIHubController)),
                interceptor
            );

            Instances.TryAdd(instanceId, client);

            return client;
        }
    }
}
