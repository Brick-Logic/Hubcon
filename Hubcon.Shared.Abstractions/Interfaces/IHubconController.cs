using Hubcon.Shared.Abstractions.Models;
using Microsoft.Extensions.Hosting;
using System.Text.Json;

namespace Hubcon.Shared.Abstractions.Interfaces
{
    public interface IBaseHubconController
    {
        Task<IOperationResponse<JsonElement>> HandleMethodTask(OperationRequest request);
        Task<IResponse> HandleMethodVoid(OperationRequest request);
        public IHubconControllerManager HubconController { get; }
    }

    public interface IBaseHubconController<TICommunicationHandler> : IBaseHubconController
        where TICommunicationHandler : ICommunicationHandler
    {
    }

    public interface IHubconClientController<TICommunicationHandler> : IBaseHubconController<TICommunicationHandler>, IHostedService
         where TICommunicationHandler : ICommunicationHandler
    {
        Task<IResponse> StartStream(string methodCode, SubscriptionRequest request);
    }
}
