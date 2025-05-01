using Autofac;
using Hubcon.Core;
using Hubcon.Core.Controllers;
using Hubcon.Core.Dummy;
using Hubcon.Core.Extensions;
using Hubcon.Core.Models;
using Hubcon.Core.Models.Interfaces;
using Hubcon.GraphQL.Client;
using Hubcon.GraphQL.Data;
using Hubcon.GraphQL.Models;
using Hubcon.GraphQL.Server;
using Hubcon.GraphQL.Subscriptions;
using Hubcon.SignalR.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace Hubcon.GraphQL.Injection
{
    public static class DependencyInjection
    {
        public static WebApplicationBuilder AddHubconGraphQL(
            this WebApplicationBuilder builder,
            Action<IControllerOptions>? controllerOptions = null,
            Action<ContainerBuilder>? additionalServices = null)
        {
            var executorBuilder = builder.Services
                .AddGraphQLServer()
                .AddQueryType<Query>()
                .AddType<BaseResponse>()
                .AddType<BaseMethodResponse>()
                .AddType<BaseJsonResponse>()
                .AddType<JsonScalarType>()
                .AddType<ObjectType<IResponse>>()    // Registra tipos como ObjectType si es necesario
                .AddType<ObjectType<IMethodResponse<JsonElement?>>>()    // Registra tipos como ObjectType si es necesario
                .AddType<InputObjectType<MethodInvokeRequest>>()  // Para cualquier tipo de entrada
                .AddProjections()
                .AddInMemorySubscriptions();

            builder.AddHubconServer(additionalServices, container =>
            {
                container.AddHubconEntrypoint<ControllerEntrypoint>();

                container.RegisterWithInjector(x => x
                    .RegisterType<HubconGraphQLClient>()
                    .As<IHubconGraphQLClient>()
                    .AsSingleton());

                container.RegisterWithInjector(container => container
                    .RegisterType<DummyCommunicationHandler>()
                    .As<ICommunicationHandler>()
                    .AsScoped());

                container.RegisterWithInjector(x => x
                    .RegisterType(typeof(ServerSubscriptionHandler))
                    .As(typeof(ISubscription))
                    .AsTransient());

                container.RegisterWithInjector(x => x
                    .RegisterType(typeof(HubconControllerManager))
                    .As<IHubconControllerManager>()
                    .AsScoped());
            });

            var controllerConfig = new ControllerOptions(executorBuilder, builder);
            controllerConfig.SetEntrypoint<ControllerEntrypoint>();
            controllerOptions?.Invoke(controllerConfig);
      
            return builder;
        }

        public static WebApplicationBuilder AddHubconGraphQLClient(this WebApplicationBuilder builder)
        {
            builder.AddHubconClientServices(container =>
            {
                container.RegisterWithInjector(x => x
                    .RegisterType<HubconGraphQLClient>()
                    .As<IHubconGraphQLClient>()
                    .AsSingleton());

                container.RegisterWithInjector(x => x
                    .RegisterType<ClientCommunicationHandler>()
                    .As<ICommunicationHandler>()
                    .AsSingleton());

                container.RegisterWithInjector(x => x
                    .RegisterType<HubconClientProvider>()
                    .As<IHubconClientProvider>()
                    .AsSingleton());

                container.RegisterWithInjector(x => x
                    .RegisterType(typeof(ClientSubscriptionHandler))
                    .As(typeof(ISubscription))
                    .AsTransient());
            });

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