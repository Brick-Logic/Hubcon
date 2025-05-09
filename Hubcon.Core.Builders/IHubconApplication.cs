using Hubcon.Core.Abstractions.Interfaces;
using Hubcon.Core.Abstractions.Standard.Interfaces;
using Hubcon.Core.Invocation;

namespace Hubcon.Core.Builders
{
    public interface IHubconClientApplication
    {
        public IServiceProvider Services { get; }
        public TICommunicationContract GetConnector<TICommunicationContract>() where TICommunicationContract : IControllerContract;
        public Task<IHubconClientController<ICommunicationHandler>> StartInstanceAsync(string? url = null, Action<string>? consoleOutput = null, CancellationToken cancellationToken = default);
        public Task StartAsync(string? url = null, Action<string>? consoleOutput = null, CancellationToken cancellationToken = default);
        public Task StartAsync(string? url = null, CancellationToken cancellationToken = default);
        public Task StopAsync(CancellationToken cancellationToken);
    }
}
