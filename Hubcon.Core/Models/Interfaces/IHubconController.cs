using Autofac;
using Hubcon.Core.MethodHandling;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using System.Text.Json;
using System.Threading.Channels;

namespace Hubcon.Core.Models.Interfaces
{
    public interface IBaseHubconController
    {
        Task<BaseJsonResponse> HandleMethodTask(MethodInvokeRequest request);
        Task<IResponse> HandleMethodVoid(MethodInvokeRequest request);
        public IHubconControllerManager HubconController { get; }
    }

    public interface IBaseHubconController<TICommunicationHandler> : IBaseHubconController
        where TICommunicationHandler : ICommunicationHandler
    {
    }

    //public interface IHubconServerController : IBaseHubconController
    //{
    //    public StreamNotificationHandler StreamNotificationHandler { get; }
    //    public ILifetimeScope ServiceProvider { get; }
    //    public Task<IResponse> ReceiveStream(string code, ChannelReader<object> reader);
    //    public IAsyncEnumerable<JsonElement?> HandleMethodStream(MethodInvokeRequest info);
    //    public void Build(WebApplication? app = null);
    //}

    public interface IHubconClientController<TICommunicationHandler> : IBaseHubconController<TICommunicationHandler>, IHostedService
         where TICommunicationHandler : ICommunicationHandler
    {
        Task<IResponse> StartStream(string methodCode, MethodInvokeRequest request);
    }

    public interface IHubconEntrypoint : IBaseHubconController
    {
        public IAsyncEnumerable<JsonElement?> HandleMethodStream(MethodInvokeRequest request);
        public void Build(WebApplication? app = null);
        IAsyncEnumerable<JsonElement?> HandleSubscription(SubscriptionRequest request);
    }

    public interface IHubconClientEntrypoint : IBaseHubconController
    {
        public void Build(WebApplication? app = null);
    }
}
