using Autofac;
using HotChocolate.Execution.Configuration;
using Hubcon.Core;
using Hubcon.Core.Models.Interfaces;
using Hubcon.GraphQL.Data;
using Hubcon.GraphQL.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.GraphQL
{
    public static class DependencyInjection
    {
        public static WebApplicationBuilder AddHubconGraphQL(
            this WebApplicationBuilder builder, 
            Action<IRequestExecutorBuilder>? controllerOptions = null, 
            Action<ContainerBuilder>? additionalServices = null)
        {
            builder.AddHubcon(additionalServices);

            var executorBuilder = builder.Services
                .AddGraphQLServer()
                .AddQueryType<Query>()
                .AddProjections();

            controllerOptions?.Invoke(executorBuilder);

            return builder;
        }

        public static IRequestExecutorBuilder AddController<TController>(this IRequestExecutorBuilder e) 
            where TController : class, IHubconServerController
        {
            e.AddMutationType<TController>();

            return e;
        }

        public static WebApplication MapHubconGraphQL(this WebApplication builder, string path)
        {
            builder.MapGraphQL(path);

            return builder;
        }
    }
}
