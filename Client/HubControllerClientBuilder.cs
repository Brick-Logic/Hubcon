using Castle.DynamicProxy;
using Hubcon.Controller;
using Hubcon.Extensions;
using Hubcon.Interceptors;
using Microsoft.AspNetCore.SignalR;

namespace Hubcon.Client
{
    public class HubControllerClientBuilder<T> where T : IHubController
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Major Code Smell", 
            "S2743:Static fields should not be used in generic types", 
            Justification = "The static field by T type is intended.")]
        protected static Dictionary<string, MethodInvokeInfo> AvailableMethods { get; } = [];
        protected IHubContext<Hub> _hub { get; }
        protected static Dictionary<string, T> Clients { get; } = [];

        public HubControllerClientBuilder(IHubContext<Hub> hub)
        {
            _hub = hub;

            if (AvailableMethods.Count == 0)
            {
                var TType = typeof(T);

                if (!TType.IsInterface)
                    throw new ArgumentException($"El tipo {typeof(T).FullName} no es una interfaz.");

                if (!typeof(IHubController).IsAssignableFrom(TType))
                    throw new NotImplementedException($"El tipo {TType.FullName} no implementa la interfaz {nameof(IHubController)} ni es un tipo derivado.");

                foreach (var method in TType.GetMethods())
                {
                    var parameters = method.GetParameters();
                    AvailableMethods.TryAdd(method.GetMethodSignature(), new MethodInvokeInfo(method.GetMethodSignature(), parameters));
                }
            }
        }

        public T GetClient(string clientId)
        {
            if (Clients.TryGetValue(clientId, out T? value))
                return value;

            var proxyGenerator = new ProxyGenerator();
            var client = (T)proxyGenerator.CreateInterfaceProxyWithTarget(
                typeof(T),
                (T)DynamicImplementationCreator.CreateImplementation(typeof(T)),
                new HubControllerClientInterceptor(_hub, clientId)
            );

            Clients.TryAdd(clientId, client);

            return client;
        }
    }
}
