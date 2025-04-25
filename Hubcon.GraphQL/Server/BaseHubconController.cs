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
    public class ControllerEntrypoint : IHubconEntrypoint
    {
        [HubconInject]
        public ILifetimeScope ServiceProvider { get; }

        [HubconInject]
        public IHubconControllerManager HubconController { get; }

        [HubconMethod(MethodType.Mutation)]
        public async Task<BaseJsonResponse> HandleMethodTask(MethodInvokeRequest info) => await HubconController.Pipeline.HandleWithResultAsync(info);    

        [HubconMethod(MethodType.Mutation)]
        public async Task<IResponse> HandleMethodVoid(MethodInvokeRequest info) => await HubconController.Pipeline.HandleWithoutResultAsync(info);

        [HubconMethod(MethodType.Subscription)]
        public IAsyncEnumerable<JsonElement?> HandleMethodStream(MethodInvokeRequest info) => HubconController.Pipeline.GetStream(info);

        public void Build(WebApplication? app = null)
        {      
        }
    }
}