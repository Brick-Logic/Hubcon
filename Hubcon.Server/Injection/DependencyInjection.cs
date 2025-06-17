using Autofac;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Core.Extensions;
using Hubcon.Server.Core.Routing;
using Hubcon.Server.Core.Subscriptions;
using Hubcon.Server.Core.Websockets.Middleware;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Entrypoint;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace Hubcon.Server.Injection
{
    public static class DependencyInjection
    {
        public static WebApplicationBuilder AddHubconServer(this WebApplicationBuilder builder, Action<ContainerBuilder>? additionalServices = null)
        {
            HubconServerBuilder.Current.AddHubconServer(builder, additionalServices, container =>
            {
                container.RegisterWithInjector(x => x.RegisterType<DefaultEntrypoint>());

                container.RegisterWithInjector(x => x
                    .RegisterGeneric(typeof(ServerSubscriptionHandler<>))
                    .As(typeof(ISubscription<>))
                    .AsTransient());
            });

            return builder;
        }

        public static WebApplicationBuilder ConfigureHubconServer(this WebApplicationBuilder builder, Action<IControllerOptions>? controllerOptions = null)
        {
            var controllerConfig = new DefaultControllerOptions(builder, HubconServerBuilder.Current);
            controllerOptions?.Invoke(controllerConfig);

            return builder;
        }

        public static WebApplication UseHubcon(this WebApplication app, string path = "operation")
        {
            //var prefix = !string.IsNullOrEmpty(path) ? $"/{path}" : "";

            //app.MapPost("/IUserService/CreateUser", async ([FromBody] OperationRequest request, DefaultEntrypoint entrypoint) =>
            //{
            //    var response = await entrypoint.HandleMethodVoid(request);
            //    return response;
            //});

            //HttpOperationRegisterer.MapTypedEndpoint

            //app.MapPost(prefix + "/" + nameof(DefaultEntrypoint.HandleMethodVoid), async ([FromBody] OperationRequest request, DefaultEntrypoint entrypoint) =>
            //{
            //    var response = await entrypoint.HandleMethodVoid(request);
            //    return response;
            //});

            //app.MapPost(prefix + "/" + nameof(DefaultEntrypoint.HandleMethodStream), async ([FromBody] OperationRequest request, DefaultEntrypoint entrypoint) =>
            //{
            //    var stream = await entrypoint.HandleMethodStream(request);
            //    return SerializeStream(stream);             
            //});

            var operationRegistry = app.Services.GetRequiredService<IOperationRegistry>();
            operationRegistry.MapControllers(app);

            app.UseWebSockets();
            app.UseMiddleware<HubconWebSocketMiddleware>();

            return app;
        }

        static async IAsyncEnumerable<JsonElement> SerializeStream(IAsyncEnumerable<object?> source)
        {
            await foreach(var element in source)
            {
                yield return JsonSerializer.SerializeToElement(element);
            }
        }
    }
}
