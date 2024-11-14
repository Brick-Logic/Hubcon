using Castle.DynamicProxy;
using Hubcon.Interceptors;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Data;

namespace Hubcon
{
    internal class ClientInstancesHolder
    {
        internal readonly static ConcurrentDictionary<Type, ConcurrentDictionary<string, IClientController>> Instances = [];
    }

    /// <summary>
    /// Allows a server to control a client given the client's interface type and a ServerHub type.
    /// This class can be injected.
    /// </summary>
    /// <typeparam name="THub"></typeparam>
    /// <typeparam name="TIClientController"></typeparam>
    internal class ClientConnectorsManager<THub, TIClientController> : ClientInstancesHolder, IClientManager<THub, TIClientController>
        where THub : ServerHub
        where TIClientController : IClientController?
    {
        IHubContext<THub> hubContext;
        THub hubReference;

        public ClientConnectorsManager(IHubContext<THub> hub)
        {
            if (!Instances.TryGetValue(typeof(THub), out var dict))
                Instances[typeof(THub)] = [];

            hubContext = hub;
        }
        public ClientConnectorsManager(THub hub)
        {
            if (!Instances.TryGetValue(typeof(THub), out var _))
                Instances[typeof(THub)] = [];

            hubReference = hub;
        }

        protected TIClientController BuildInstance(string instanceId)
        {
            var proxyGenerator = new ProxyGenerator();

            ClientControllerConnectorInterceptor interceptor = hubReference == null
                ? new ClientControllerConnectorInterceptor(hubContext, instanceId)
                : new ClientControllerConnectorInterceptor(hubReference!, instanceId);

            return (TIClientController)proxyGenerator.CreateInterfaceProxyWithTarget(
                typeof(TIClientController),
                (TIClientController)DynamicImplementationCreator.CreateImplementation(typeof(TIClientController)),
                interceptor
            );
        }

        protected bool TryGetInstance(Type hubType, string instanceId, out TIClientController? instance)
        {
            instance = default;

            Instances.TryGetValue(hubType, out var dict);

            if (!dict!.TryGetValue(instanceId, out var _))
                dict.TryAdd(instanceId, BuildInstance(instanceId)!);

            dict.TryGetValue(instanceId, out IClientController? controllerValue);
            instance = (TIClientController?)controllerValue;
            return true;
        }

        public TIClientController GetClient(string instanceId)
        {
            if (!TryGetInstance(typeof(THub), instanceId, out TIClientController? value))
                return default;

            return value;
        }

        public IEnumerable<string> GetAllClients()
        {
            return ServerHub.GetClients(typeof(THub)).Select(x => x.Id);
        }

        public void RemoveInstance(Type hubType, string instanceId)
        {
            if (Instances.TryGetValue(hubType, out var dict))
                dict.TryRemove(instanceId, out _);
        }
    }
}
