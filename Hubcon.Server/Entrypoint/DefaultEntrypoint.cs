using Autofac;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Abstractions.Standard.Attributes;
using System.Text.Json;

namespace Hubcon.Server.Entrypoint
{
    public class DefaultEntrypoint
    {
        [HubconInject]
        public ILifetimeScope ServiceProvider { get; }

        [HubconInject]
        public IHubconControllerManager HubconController { get; }

        public async Task<IOperationResponse<JsonElement>> HandleMethodTask(OperationRequest request)
            => await HubconController.Pipeline.HandleWithResultAsync(request);

        public async Task<IResponse> HandleMethodVoid(OperationRequest request)
        {
            var res = await HubconController.Pipeline.HandleWithoutResultAsync(request);
            return res;
        }

        public IAsyncEnumerable<JsonElement> HandleMethodStream(OperationRequest request)
            => HubconController.Pipeline.GetStream(request);

        public IAsyncEnumerable<JsonElement> HandleSubscription(SubscriptionRequest request)
            => HubconController.Pipeline.GetSubscription(request);

        public void Build()
        {
        }
    }
}
