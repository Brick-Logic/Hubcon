using Hubcon.Core.Models.Interfaces;

namespace Hubcon.Core.Builders
{
    public class HubconApplication : IHubconClientApplication
    {
        public IServiceProvider Services { get; }

        public IHubconClientController<ICommunicationHandler>? Controller { get; set; }

        internal HubconApplication(IServiceProvider services)
        {
            Services = services;
        }

        public static IHubconApplicationBuilder CreateBuilder(string[] args)
        {
            var builder = new HubconApplicationBuilder();

            return builder;
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public TICommunicationContract GetConnector<TICommunicationContract>() where TICommunicationContract : IControllerContract
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
