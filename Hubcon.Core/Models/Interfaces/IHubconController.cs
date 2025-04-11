using Autofac;
using Hubcon.Core.MethodHandling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Threading.Channels;

namespace Hubcon.Core.Models.Interfaces
{
    public interface IBaseHubconController
    {
        Task<MethodResponse> HandleMethodTask(MethodInvokeRequest info);
        Task HandleMethodVoid(MethodInvokeRequest info);
        public IHubconControllerManager HubconController { get; }
    }

    public interface IBaseHubconController<TICommunicationHandler> : IBaseHubconController
        where TICommunicationHandler : ICommunicationHandler
    {
    }

    public interface IHubconServerController : IBaseHubconController
    {
        public StreamNotificationHandler StreamNotificationHandler { get; }
        public ILifetimeScope ServiceProvider { get; }
        public Task ReceiveStream(string code, ChannelReader<object> reader);
        public IAsyncEnumerable<object> HandleMethodStream(MethodInvokeRequest info);
        public void Build();
    }

    public interface IHubconClientController<TICommunicationHandler> : IBaseHubconController<TICommunicationHandler>, IHostedService
         where TICommunicationHandler : ICommunicationHandler
    {
        Task StartStream(string methodCode, MethodInvokeRequest info);
    }
}
