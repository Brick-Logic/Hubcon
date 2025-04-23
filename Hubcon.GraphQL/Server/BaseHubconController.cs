using Autofac;
using Hubcon.Core.Injectors.Attributes;
using Hubcon.Core.MethodHandling;
using Hubcon.Core.Models;
using Hubcon.Core.Models.Interfaces;
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
        [GraphQLIgnore]
        [HubconInject]
        public StreamNotificationHandler StreamNotificationHandler { get; }

        [GraphQLIgnore]
        [HubconInject]
        public ILifetimeScope ServiceProvider { get; }

        [GraphQLIgnore]
        [HubconInject]
        public IHubconControllerManager HubconController { get; }

        [GraphQLIgnore]
        public void Build(WebApplication? app = null)
        {
            
        }

        [GraphQLIgnore]
        public IAsyncEnumerable<JsonElement?> HandleMethodStream(MethodInvokeRequest info)
        {
            throw new NotImplementedException();
        }

        public Task<IMethodResponse> HandleMethodTask(MethodInvokeRequest info)
        {
            throw new NotImplementedException();
        }       

        [GraphQLIgnore]
        public Task HandleMethodVoid(MethodInvokeRequest info)
        {
            throw new NotImplementedException();
        }

        [GraphQLIgnore]
        public Task ReceiveStream(string code, ChannelReader<object> reader)
        {
            throw new NotImplementedException();
        }
    }

    public class DeleteResult
    {
        public bool Result { get; set; }
        public TestData DeletedEntity { get; set; }

        public DeleteResult(bool result, TestData deletedEntity)
        {
            Result = result;
            DeletedEntity = deletedEntity;
        }

        public DeleteResult()
        {

        }
    }
}
