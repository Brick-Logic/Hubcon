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
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Hubcon.Server.Injection
{
    public static class DependencyInjection
    {
        public static WebApplicationBuilder AddHubcon(this WebApplicationBuilder builder, Action<ContainerBuilder>? additionalServices = null)
        {
            HubconServerBuilder.Current.AddHubconServer(builder, additionalServices, container =>
            {
                container.RegisterWithInjector(x => x.RegisterType<DefaultEntrypoint>());

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

        public static WebApplicationBuilder UseHubcon(this WebApplicationBuilder builder, Action<IControllerOptions>? controllerOptions = null)
        {
            var controllerConfig = new DefaultControllerOptions(builder, HubconServerBuilder.Current);
            controllerOptions?.Invoke(controllerConfig);

            return builder;
        }

        public static WebApplication UseHubcon(this WebApplication app, string path = "operation")
        {
            var prefix = !string.IsNullOrEmpty(path) ? $"/{path}" : "";

            app.MapPost(prefix + "/" + nameof(DefaultEntrypoint.HandleMethodTask), async ([FromBody] OperationRequest request, DefaultEntrypoint entrypoint) =>
            {
                var response = await entrypoint.HandleMethodTask(request);
                return response;
            });

            app.MapPost(prefix + "/" + nameof(DefaultEntrypoint.HandleMethodVoid), async ([FromBody] OperationRequest request, DefaultEntrypoint entrypoint) =>
            {
                var response = await entrypoint.HandleMethodVoid(request);
                return response;
            });

            app.MapPost(prefix + "/" + nameof(DefaultEntrypoint.HandleMethodStream), async ([FromBody] OperationRequest request, DefaultEntrypoint entrypoint) =>
            {
                var stream = await entrypoint.HandleMethodStream(request);
                return SerializeStream(stream);             
            });

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
