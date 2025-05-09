using Autofac;
using Hubcon.Core.Invocation;
using Microsoft.AspNetCore.SignalR;

namespace Hubcon.SignalR.HubActivator
{
    public class HubconHubActivator<T> : IHubActivator<T> where T : Hub, IHubconEntrypoint
    {
        private readonly ILifetimeScope _serviceProvider;

        public HubconHubActivator(ILifetimeScope serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public T Create()
        {
            // Crear un scope para obtener servicios scoped
            var hubInstance = _serviceProvider.Resolve<T>();

            hubInstance?.Build();

            return hubInstance!;
        }

        public void Release(T hub)
        {
            
        }
    }
}
