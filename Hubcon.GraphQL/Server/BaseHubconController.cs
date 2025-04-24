using Autofac;
using Hubcon.Core.Injectors.Attributes;
using Hubcon.Core.MethodHandling;
using Hubcon.Core.Models;
using Hubcon.Core.Models.Interfaces;
using Hubcon.GraphQL.CustomAttributes;
using Hubcon.GraphQL.Data;
using Microsoft.AspNetCore.Builder;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Hubcon.GraphQL.Server
{
    public abstract class BaseHubconController : IHubconServerController
    {
        [HubconInject]
        public StreamNotificationHandler StreamNotificationHandler { get; }

        [HubconInject]
        public ILifetimeScope ServiceProvider { get; }

        [HubconInject]
        public IHubconControllerManager HubconController { get; }

        public void Build(WebApplication? app = null) => HubconController.Pipeline.RegisterMethods(GetType());

        [HubconMethod(MethodType.Mutation)]
        public async Task<BaseJsonResponse> HandleMethodTask(MethodInvokeRequest info)
            => (BaseJsonResponse)await HubconController.Pipeline.HandleWithResultAsync(this, info);

        [HubconMethod(MethodType.Mutation)]
        public async Task<IResponse> HandleMethodVoid(MethodInvokeRequest info)
            => await HubconController.Pipeline.HandleWithoutResultAsync(this, info);
        
        public async Task<IResponse> ReceiveStream(string code, ChannelReader<object> reader)
            => await StreamNotificationHandler.NotifyStream(code, reader);

        [HubconMethod(MethodType.Subscription)]
        public IAsyncEnumerable<JsonElement?> HandleMethodStream(MethodInvokeRequest info)
            => HubconController.Pipeline.GetStream(this, info);
    }

    public class VoidTaskResult : IResponse
    {
        public bool Success { get; set; }

        public VoidTaskResult(bool success)
        {
            Success = success;
        }
    }
}
