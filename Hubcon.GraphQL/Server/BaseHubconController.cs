using Autofac;
using Hubcon.Core.Injectors.Attributes;
using Hubcon.Core.MethodHandling;
using Hubcon.Core.Models;
using Hubcon.Core.Models.Interfaces;
using Hubcon.GraphQL.Data;
using Hubcon.GraphQL.Models.CustomAttributes;
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
    public class ControllerEntrypoint : IHubconEntrypoint
    {
        [HubconInject]
        public ILifetimeScope ServiceProvider { get; }

        [HubconInject]
        public IHubconControllerManager HubconController { get; }

        [HubconMethod(MethodType.Mutation)]
        public async Task<BaseJsonResponse> HandleMethodTask(MethodInvokeRequest request) => await HubconController.Pipeline.HandleWithResultAsync(request);    

        [HubconMethod(MethodType.Mutation)]
        public async Task<IResponse> HandleMethodVoid(MethodInvokeRequest request) => await HubconController.Pipeline.HandleWithoutResultAsync(request);

        [HubconMethod(MethodType.Subscription)]
        public IAsyncEnumerable<JsonElement?> HandleMethodStream(MethodInvokeRequest request) => HubconController.Pipeline.GetStream(request);

        public void Build(WebApplication? app = null)
        {      
        }
    }
}