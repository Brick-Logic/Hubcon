using Hubcon.Core.Injectors;
using Hubcon.Core.Models.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Core.Builders
{
    public class HubconApplication : IHubconClientApplication
    {
        public IServiceProvider Services { get; }

        public IHubconClientController<ICommunicationHandler> Controller { get; }

        internal HubconApplication(IServiceProvider services)
        {
            Services = services;
        }

        public static IHubconApplicationBuilder CreateBuilder<T>(string[] args) where T : IHubconClientController<ICommunicationHandler>
        {
            var builder = new HubconApplicationBuilder();
            //builder.Services.AddSingleton<>();

            return builder;
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public TICommunicationContract GetConnector<TICommunicationContract>() where TICommunicationContract : IHubconControllerContract
        {
            throw new NotImplementedException();
        }

        public Task<IHubconClientController<ICommunicationHandler>> StartInstanceAsync(string? url = null, Action<string>? consoleOutput = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task StartAsync(string? url = null, Action<string>? consoleOutput = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task StartAsync(string? url = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        Task<Models.Interfaces.IHubconClientController<ICommunicationHandler>> IHubconClientApplication.StartInstanceAsync(string? url, Action<string>? consoleOutput, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
