using Hubcon.Core.Injectors;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Core.Models.Interfaces
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
