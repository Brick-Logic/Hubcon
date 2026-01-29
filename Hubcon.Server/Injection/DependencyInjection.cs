using Hubcon.Server;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Core.EndpointDocumentation;
using Hubcon.Server.Core.Routing;
using Hubcon.Server.Core.Subscriptions;
using Hubcon.Server.Core.Websockets.Middleware;
using Hubcon.Server.Injection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;
using System.Text.Json.Serialization;

namespace Hubcon
{
    internal sealed class RemoveNullableSchemaFilter : ISchemaFilter
    {
        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            if (schema.Type == "string")
            {
                schema.Nullable = false;
                schema.Example = new OpenApiString("ejemplo");
            }          

            if (schema.Properties != null)
            {
                foreach (var prop in schema.Properties.Values.Where(x => x.Type == "string"))
                {
                    prop.Nullable = false;
                }
            }
        }
    }

    public static class DependencyInjection
    {
        public static WebApplicationBuilder AddHubconServer(this WebApplicationBuilder builder, Action<IServiceCollection>? additionalServices = null)
        {
            builder.Services.AddControllers();
            builder.Services.ConfigureHttpJsonOptions(options =>
            {
                options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            });

            builder.Services.AddSwaggerGen(options =>
            {
                options.SupportNonNullableReferenceTypes();
                options.SchemaGeneratorOptions.SupportNonNullableReferenceTypes = true;

                // Esta es la clave - configurar para que no genere tipos nullable automáticamente
                options.UseAllOfToExtendReferenceSchemas();
                options.UseOneOfForPolymorphism();

                // Filtro personalizado para limpiar los schemas
                options.OperationFilter<RemoveNullableTypesOperationFilter>();

                options.SchemaFilter<RemoveNullableSchemaFilter>();
            });

            builder.Services.ConfigureSwaggerGen(options =>
            {
                options.MapType<string>(() => new OpenApiSchema
                {
                    Type = "string",
                    Nullable = false,
                    Example = new OpenApiString("string")
                });
            });

            ServerBuilder.Current.AddHubconServer(builder, additionalServices, container =>
            {
                container.AddTransient(typeof(ISubscription<>), typeof(ServerSubscriptionHandler<>));
            });

            return builder;
        }

        public static WebApplicationBuilder ConfigureHubconServer(this WebApplicationBuilder builder, Action<IServerOptions>? controllerOptions = null)
        {
            var controllerConfig = new DefaultServerOptions(builder, ServerBuilder.Current);
            controllerOptions?.Invoke(controllerConfig);

            return builder;
        }

        public static WebApplication UseHubconHttpEndpoints(this WebApplication app)
        {
            var operationRegistry = app.Services.GetRequiredService<IOperationRegistry>();

            operationRegistry.MapTransport<HttpTransport>(app, (operations, app) =>
            {
                foreach (var operation in operations)
                {
                    if (operation.Value.Kind != OperationKind.CallMethod && operation.Value.Kind != OperationKind.InvokeMethod && operation.Value.Kind != OperationKind.Stream)
                        continue;

                    if (operation.Value.MemberInfo is MethodInfo)
                    {
                        app.MapTypedEndpoint(operation.Value);
                    }
                }
            });

            return app;
        }

        public static WebApplication UseHubconTransport<T>(this WebApplication app, Action<IReadOnlyDictionary<string, IOperationBlueprint>, WebApplication>? configurator = null) where T : HubconTransport, new()
        {
            var operationRegistry = app.Services.GetRequiredService<IOperationRegistry>();
            operationRegistry.MapTransport<T>(app, configurator);
            return app;
        }

        public static WebApplication UseHubconWebsocketEndpoints(this WebApplication app, WebSocketOptions? options = null)
        {
            var operationRegistry = app.Services.GetRequiredService<IOperationRegistry>();

            if(options != null)
                app.UseWebSockets(options);
            else
                app.UseWebSockets();

            app.UseMiddleware<HubconWebSocketMiddleware>();

            operationRegistry.MapTransport<WebSocketTransport>(app);

            return app;
        }
    }
}
