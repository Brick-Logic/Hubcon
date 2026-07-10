using Hubcon.Server;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Core.Cache;
using Hubcon.Server.Core.Routing;
using Hubcon.Server.Core.Websockets.Middleware;
using Hubcon.Server.Injection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;
using System.Text.Json.Serialization;
using Hubcon.Shared.Core.Serialization;
using Microsoft.AspNetCore.Http;

#pragma warning disable CS1591

namespace Hubcon
{
    // internal sealed class RemoveNullableSchemaFilter : ISchemaFilter
    // {
    //     public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    //     {
    //         if (schema.Type == "string")
    //         {
    //             schema.Nullable = false;
    //             schema.Example = new OpenApiString("string");
    //         }          
    //
    //         if (schema.Properties != null)
    //         {
    //             foreach (var prop in schema.Properties.Values.Where(x => x.Type == "string"))
    //             {
    //                 prop.Nullable = false;
    //             }
    //         }
    //     }
    // }

    public static class DependencyInjection
    {
        public static WebApplicationBuilder AddHubconServer(this WebApplicationBuilder builder, Action<IServerOptions>? controllerOptions = null)
        {
            builder.Services.ConfigureHttpJsonOptions(options =>
            {
                options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                options.SerializerOptions.TypeInfoResolver = DynamicConverter.JsonSerializerOptions.TypeInfoResolver!;
            });
            
            builder.Services.AddControllers();
            
            ServerBuilder.Current.AddHubconServer(builder);

            builder.ConfigureHubconServer(controllerOptions);

            ServerBuilder.Current.ConfigureServices(services =>
            {
                if(services.Any(x => x.ServiceType == typeof(IOperationCache)))
                {
                    return;
                }

                services.TryAddSingleton<IMemoryCache, MemoryCache>();
                services.TryAddSingleton<IOperationCache, DefaultMemoryCache>();
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
                        app.RegisterEndpoint(operation.Value);
                    }
                }
            });
            
            app.UseStatusCodePages(async context =>
            {
                if (context.HttpContext.Response.StatusCode == StatusCodes.Status404NotFound)
                {
                    context.HttpContext.Response.ContentType = "application/json";

                    IResponse notFoundResponse = HubconResponse.NotFound(null!, "Resource or endpoint not found.", null!);
                    await context.HttpContext.Response.WriteAsJsonAsync(notFoundResponse);
                }
            });

            return app;
        }

        public static WebApplication UseHubconTransport<T>(this WebApplication app, Action<IReadOnlyDictionary<string, IOperationBlueprint>, WebApplication>? configurator = null) where T : HubconTransportAttribute, new()
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