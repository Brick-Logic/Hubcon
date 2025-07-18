using Autofac;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Core.EndpointDocumentation;
using Hubcon.Server.Core.Entrypoint;
using Hubcon.Server.Core.Extensions;
using Hubcon.Server.Core.Subscriptions;
using Hubcon.Server.Core.Websockets.Middleware;
using Hubcon.Shared.Abstractions.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hubcon.Server.Injection
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
        public static WebApplicationBuilder AddHubconServer(this WebApplicationBuilder builder, Action<ContainerBuilder>? additionalServices = null)
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
                container.RegisterWithInjector(x => x.RegisterType<DefaultEntrypoint>());

                container.RegisterWithInjector(x => x
                    .RegisterGeneric(typeof(ServerSubscriptionHandler<>))
                    .As(typeof(ISubscription<>))
                    .AsTransient());
            });

            return builder;
        }

        public static WebApplicationBuilder ConfigureHubconServer(this WebApplicationBuilder builder, Action<IServerOptions>? controllerOptions = null)
        {
            var controllerConfig = new DefaultServerOptions(builder, ServerBuilder.Current);
            controllerOptions?.Invoke(controllerConfig);

            return builder;
        }

        public static WebApplication MapHubconControllers(this WebApplication app)
        {
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
            }

            var operationRegistry = app.Services.GetRequiredService<IOperationRegistry>();
            operationRegistry.MapControllers(app);

            return app;
        }

        public static WebApplication UseHubconWebsockets(this WebApplication app)
        {
            app.UseWebSockets();
            app.UseMiddleware<HubconWebSocketMiddleware>();

            return app;
        }

        //public static WebApplication UseHubcon(this WebApplication app, string path = "operation")
        //{
        //    var operationRegistry = app.Services.GetRequiredService<IOperationRegistry>();
        //    operationRegistry.MapControllers(app);

        //    app.UseWebSockets();
        //    app.UseMiddleware<HubconWebSocketMiddleware>();

        //    return app;
        //}

        static async IAsyncEnumerable<JsonElement> SerializeStream(IAsyncEnumerable<object?> source)
        {
            await foreach(var element in source)
            {
                yield return JsonSerializer.SerializeToElement(element);
            }
        }
    }
}
