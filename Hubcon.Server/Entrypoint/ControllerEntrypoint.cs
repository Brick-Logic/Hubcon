using Autofac;
using Hubcon.Server.Models.CustomAttributes;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Abstractions.Standard.Attributes;
using Microsoft.AspNetCore.Builder;
using System.Text.Json;

namespace Hubcon.Server.Entrypoint
{
    public class ControllerEntrypoint : IHubconEntrypoint
    {
        [HubconInject]
        public ILifetimeScope ServiceProvider { get; }

        [HubconInject]
        public IHubconControllerManager HubconController { get; }

        [HubconMethod(MethodType.Mutation)]
        public async Task<IOperationResponse<JsonElement>> HandleMethodTask(OperationRequest request)
            => await HubconController.Pipeline.HandleWithResultAsync(request);

        [HubconMethod(MethodType.Mutation)]
        public async Task<IResponse> HandleMethodVoid(OperationRequest request)
        {
            var res = await HubconController.Pipeline.HandleWithoutResultAsync(request);
            return res;
        }

        [HubconMethod(MethodType.Subscription)]
        public IAsyncEnumerable<JsonElement> HandleMethodStream(OperationRequest request)
            => HubconController.Pipeline.GetStream(request);

        [HubconMethod(MethodType.Subscription)]
        public IAsyncEnumerable<JsonElement> HandleSubscription(SubscriptionRequest request)
            => HubconController.Pipeline.GetSubscription(request);
        
        public void Build()
        {
        }
    }
}