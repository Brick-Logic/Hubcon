using Autofac;
using HotChocolate.Execution.Configuration;
using Hubcon.Server.Core.Controllers;
using Hubcon.Server.Core.Dummy;
using Hubcon.Server.Core.Extensions;
using Hubcon.Server.Core.Websockets.Middleware;
using Hubcon.Server.Data;
using Hubcon.Server.Entrypoint;
using Hubcon.Server.Interceptors;
using Hubcon.Server.Models;
using Hubcon.Server.Subscriptions;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace Hubcon.Server.Injection
{
    public static class GraphQLDependencyInjection
    {
        private static IRequestExecutorBuilder? requestExecutorBuilder;

        public static WebApplicationBuilder AddHubconGraphQL(
            this WebApplicationBuilder builder,
            Action<ContainerBuilder>? additionalServices = null)
        {
            HubconServerBuilder.Current.AddHubconServer(builder, additionalServices, container =>
            {
                container.RegisterWithInjector(x => x.RegisterType<GraphQLEntrypoint>());

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

            return builder;
        }

        public static WebApplicationBuilder UseHubconGraphQL(this WebApplicationBuilder builder, Action<IControllerOptions>? controllerOptions = null)
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
                .AddType<InputObjectType<OperationRequest>>()
                .AddType<InputObjectType<SubscriptionRequest>>()
                .DisableIntrospection(false)
                .AddInMemorySubscriptions()
                .AddSocketSessionInterceptor<SocketSessionInterceptor>();

            var controllerConfig = new GraphQLControllerOptions(requestExecutorBuilder!, builder, HubconServerBuilder.Current);
            controllerConfig.SetEntrypoint<GraphQLEntrypoint>();
            controllerOptions?.Invoke(controllerConfig);


            return builder;
        }

        public static WebApplicationBuilder UseHubconGraphQL2(this WebApplicationBuilder builder, Action<IControllerOptions>? controllerOptions = null)
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
                .AddType<InputObjectType<OperationRequest>>()
                .AddType<InputObjectType<SubscriptionRequest>>()
                .DisableIntrospection(false)
                .AddInMemorySubscriptions()
                .AddSocketSessionInterceptor<SocketSessionInterceptor>();

            var controllerConfig = new GraphQLControllerOptions(requestExecutorBuilder!, builder, HubconServerBuilder.Current);
            controllerConfig.SetEntrypoint<GraphQLEntrypoint>();
            controllerOptions?.Invoke(controllerConfig);


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