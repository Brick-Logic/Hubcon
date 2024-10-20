using Castle.DynamicProxy;
using Hubcon.Controller;
using Hubcon.Default;
using Hubcon.Extensions;
using Hubcon.Interceptors;
using Hubcon.Models;
using Microsoft.AspNetCore.SignalR;

namespace Hubcon.Client
{
    public class HubControllerClient<TIHubController, THub> 
        where TIHubController : IHubController
        where THub : Hub
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Major Code Smell", 
            "S2743:Static fields should not be used in generic types", 
            Justification = "The static field by T type is intended.")]
        internal static Dictionary<string, MethodInvokeInfo> AvailableMethods { get; } = [];
        protected IHubContext<THub> _hubContext { get; }
        protected static Dictionary<string, TIHubController> Clients { get; } = [];

        public HubControllerClient(IHubContext<THub> hubContext)
        {
            _hubContext = hubContext;

            if (AvailableMethods.Count == 0)
            {
                var TType = typeof(TIHubController);

                if (!TType.IsInterface)
                    throw new ArgumentException($"El tipo {typeof(TIHubController).FullName} no es una interfaz.");

                if (!typeof(IHubController).IsAssignableFrom(TType))
                    throw new NotImplementedException($"El tipo {TType.FullName} no implementa la interfaz {nameof(IHubController)} ni es un tipo derivado.");

                foreach (var method in TType.GetMethods())
                {
                    var parameters = method.GetParameters();
                    AvailableMethods.TryAdd(method.GetMethodSignature(), new MethodInvokeInfo(method.GetMethodSignature(), parameters));
                }
            }
        }

        public TIHubController GetInstance(string controllerId)
        {
            if (Clients.TryGetValue(controllerId, out TIHubController? value))
                return value;

            var proxyGenerator = new ProxyGenerator();
            var client = (TIHubController)proxyGenerator.CreateInterfaceProxyWithTarget(
                typeof(TIHubController),
                (TIHubController)DynamicImplementationCreator.CreateImplementation(typeof(TIHubController)),
                new HubControllerClientInterceptor(_hubContext, controllerId)
            );

            Clients.TryAdd(controllerId, client);

            return client;
        }
    }
}
