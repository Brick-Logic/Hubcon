using Hubcon.Core.Abstractions.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using System.Text.Json;

namespace Hubcon.Core.Invocation
{
    public interface IBaseHubconController
    {
        Task<IOperationResponse<JsonElement>> HandleMethodTask(MethodInvokeRequest request);
        Task<IResponse> HandleMethodVoid(MethodInvokeRequest request);
        public IHubconControllerManager HubconController { get; }
    }

    public interface IBaseHubconController<TICommunicationHandler> : IBaseHubconController
        where TICommunicationHandler : ICommunicationHandler
    {
    }

    public interface IHubconClientController<TICommunicationHandler> : IBaseHubconController<TICommunicationHandler>, IHostedService
         where TICommunicationHandler : ICommunicationHandler
    {
        Task<IResponse> StartStream(string methodCode, MethodInvokeRequest request);
    }
}
