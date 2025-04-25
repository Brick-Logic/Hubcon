using Autofac;
using Hubcon.Core.Controllers;
using Hubcon.Core.Injectors;
using Hubcon.Core.MethodHandling;
using Hubcon.Core.Models.Interfaces;
using Hubcon.Core.Tools;
using Hubcon.SignalR.Server;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using System.Linq.Expressions;
using System.Reflection;

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
