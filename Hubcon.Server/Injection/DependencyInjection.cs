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
using Microsoft.Extensions.Logging;

#pragma warning disable CS1591

namespace Hubcon
{
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

        public static WebApplication MapHubconTransport<TTransport, TSettings, TRegisterer>(this WebApplication app) 
            where TTransport: HubconTransportAttribute<TSettings>, new()
            where TSettings: class, ITransportSettings, new()
            where TRegisterer: TransportRegisterer<TTransport, TSettings>, new()
        {
            var operationRegistry = app.Services.GetRequiredService<IOperationRegistry>();
            var options = app.Services.GetRequiredService<IInternalServerOptions>();
            var registerer = new TRegisterer();
            
            operationRegistry.MapTransport<TTransport>(app, (operations, app) =>
            {
                registerer.Setup(app);
                var settings = options.GetTransportSettings<TTransport>();
                
                foreach (var operation in operations)
                {
                    switch (operation.Value.Kind)
                    {
                        case OperationKind.CallMethod when !settings.CallOperationEnabled:
                            registerer.RegisterCallOperation(operation.Value, app);
                            break;
                        case OperationKind.InvokeMethod when !settings.InvokeOperationEnabled:
                            registerer.RegisterInvokeOperation(operation.Value, app);
                            break;
                        case OperationKind.Stream when !settings.StreamOperationEnabled:
                            registerer.RegisterStreamOperation(operation.Value, app);
                            break;
                        case OperationKind.Ingest when !settings.IngestOperationEnabled:
                            registerer.RegisterIngest(operation.Value, app);
                            break;
                        default:
                            continue;
                    }
                }

                registerer.PostRegister(app);
            });

            return app;
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
            
            app.Use(async (context, next) =>
            {
                try
                {
                    await next(context);
                }
                catch (BadHttpRequestException)
                {
                    context.Response.StatusCode = 400;
                    context.Response.ContentType = "application/json";

                    var response = HubconResponse.BadRequest();
                    await context.Response.WriteAsJsonAsync(response);
                }
                catch (UnauthorizedAccessException)
                {
                    context.Response.StatusCode = 403;
                    context.Response.ContentType = "application/json";

                    var response = HubconResponse.Unauthorized();
                    await context.Response.WriteAsJsonAsync(response);
                }
                catch (Exception ex)
                {
                    context.Response.StatusCode = 500;
                    context.Response.ContentType = "application/json";
                    var logger = context.RequestServices.GetRequiredService<ILogger<IHubconMiddleware>>();
                    var options = context.RequestServices.GetRequiredService<IInternalServerOptions>();
                    logger.LogError("{Message}", ex.Message);

                    HubconResponse response;

                    if (options.DetailedErrorsEnabled)
                    {
                        response = HubconResponse.InternalError(ex, ex.Message);
                    }
                    else
                    {
                        response = HubconResponse.InternalError();
                    }
                    await context.Response.WriteAsJsonAsync(response);
                }
            });
            
            app.UseStatusCodePages(async context =>
            {
                if (context.HttpContext.Response.StatusCode == StatusCodes.Status404NotFound)
                {
                    context.HttpContext.Response.ContentType = "application/json";

                    var response = HubconResponse.NotFound();
                    await context.HttpContext.Response.WriteAsJsonAsync(response);
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