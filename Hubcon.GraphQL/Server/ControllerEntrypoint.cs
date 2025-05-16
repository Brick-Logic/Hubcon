using Autofac;
using Hubcon.Core.Abstractions.Interfaces;
using Hubcon.Core.Abstractions.Standard.Attributes;
using Hubcon.Core.Invocation;
using Hubcon.Core.Subscriptions;
using Hubcon.GraphQL.Models.CustomAttributes;
using Microsoft.AspNetCore.Builder;
using System.Text.Json;

namespace Hubcon.GraphQL.Server
{
    public class ControllerEntrypoint : IHubconEntrypoint
    {
        [HubconInject]
        public ILifetimeScope ServiceProvider { get; }

        [HubconInject]
        public IHubconControllerManager HubconController { get; }

        public ControllerEntrypoint()
        {
            
        }

        [HubconMethod(MethodType.Mutation)]
        public async Task<IOperationResponse<JsonElement>> HandleMethodTask(MethodInvokeRequest request)
            => await HubconController.Pipeline.HandleWithResultAsync(request);

        [HubconMethod(MethodType.Mutation)]
        public async Task<IResponse> HandleMethodVoid(MethodInvokeRequest request)
        {
            var res = await HubconController.Pipeline.HandleWithoutResultAsync(request);
            return res;
        }

        [HubconMethod(MethodType.Subscription)]
        public IAsyncEnumerable<JsonElement?> HandleMethodStream(MethodInvokeRequest request)
            => HubconController.Pipeline.GetStream(request);

        [HubconMethod(MethodType.Subscription)]
        public IAsyncEnumerable<JsonElement?> HandleSubscription(SubscriptionRequest request)
            => HubconController.Pipeline.GetSubscription(request);
        

        public void Build(WebApplication? app = null)
        {
        }
    }
}