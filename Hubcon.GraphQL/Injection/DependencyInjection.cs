using Autofac;
using Hubcon.Core.Abstractions.Interfaces;
using Hubcon.Core.Controllers;
using Hubcon.Core.Dummy;
using Hubcon.Core.Extensions;
using Hubcon.Core.Invocation;
using Hubcon.Core.Subscriptions;
using Hubcon.GraphQL.Client;
using Hubcon.GraphQL.Data;
using Hubcon.GraphQL.Models;
using Hubcon.GraphQL.Server;
using Hubcon.GraphQL.Subscriptions;
using Hubcon.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace Hubcon.GraphQL.Injection
{
    public static class DependencyInjection
    {
        public static WebApplicationBuilder AddHubcon(
            this WebApplicationBuilder builder,
            Action<IControllerOptions>? controllerOptions = null,
            Action<ContainerBuilder>? additionalServices = null)
        {
            var executorBuilder = builder.Services
                .AddGraphQLServer()
                .AddQueryType<Query>()
                .AddType<BaseResponse>()
                .AddType<BaseOperationResponse>()
                .AddType<BaseJsonResponse>()
                .AddType<JsonScalarType>()
                .AddType<ObjectType<IResponse>>()
                .AddType<ObjectType<IOperationResponse<JsonElement>>>()
                .AddType<InputObjectType<MethodInvokeRequest>>()
                .AddType<InputObjectType<SubscriptionRequest>>()
                .AddProjections()
                .AddInMemorySubscriptions();


            HubconServerBuilder.Current.AddHubconServer(builder, additionalServices, container =>
            {
                container.RegisterWithInjector(x => x.RegisterType<ControllerEntrypoint>());

                container.RegisterWithInjector(x => x
                    .RegisterType<HubconGraphQLClient>()
                    .As<IHubconClient>()
                    .AsSingleton());

                container.RegisterWithInjector(container => container
                    .RegisterType<DummyCommunicationHandler>()
                    .As<ICommunicationHandler>()
                    .AsScoped());

                container.RegisterWithInjector(x => x
                    .RegisterGeneric(typeof(ServerSubscriptionHandler<>))
                    .As(typeof(ISubscription<>))
                    .AsTransient());

                container.RegisterWithInjector(x => x
                    .RegisterType(typeof(HubconControllerManager))
                    .As<IHubconControllerManager>()
                    .AsScoped());
            });


            var controllerConfig = new ControllerOptions(executorBuilder, builder, HubconServerBuilder.Current);
            controllerConfig.SetEntrypoint<ControllerEntrypoint>();
            controllerOptions?.Invoke(controllerConfig);
      
            return builder;
        }

        public static WebApplicationBuilder UseAuthenticationManager<T>(this WebApplicationBuilder builder)
            where T : IAuthenticationManager
        {
            HubconServerBuilder.Current.UseAuthenticationManager<T>();
            return builder;

        }

        public static WebApplicationBuilder UseAuthenticationManager(this WebApplicationBuilder builder, Type authenticationManagerType)
        {
            HubconServerBuilder.Current.UseAuthenticationManager(authenticationManagerType);
            return builder;
        }

        public static WebApplication MapHubconGraphQL(this WebApplication app, string path)
        {
            app.UseWebSockets();
            app.MapGraphQL(path);

            return app;
        }
    }
}