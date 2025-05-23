using Autofac;
using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Client.Builder;
using Hubcon.Client.Integration.Client;
using Hubcon.Client.Integration.Subscriptions;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Components.Extensions;
using Microsoft.AspNetCore.Builder;

namespace Hubcon.Client
{
    public static class DependencyInjection
    {
        public static WebApplicationBuilder AddHubconClient(this WebApplicationBuilder builder)
        {
            HubconClientBuilder.Current.AddHubconClientServices(builder, container =>
            {
                container.RegisterWithInjector(x => x
                    .RegisterType<HubconClient>()
                    .As<IHubconClient>()
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
                    .RegisterGeneric(typeof(ClientSubscriptionHandler<>))
                    .As(typeof(ISubscription<>))
                    .AsTransient());
            });

            return builder;
        }

        public static WebApplicationBuilder AddRemoteServerModule<TRemoteServerModule>(this WebApplicationBuilder builder)
             where TRemoteServerModule : IRemoteServerModule, new()
        {
            HubconClientBuilder.Current.AddRemoteServerModule<TRemoteServerModule>(builder);

            return builder;
        }

        public static WebApplicationBuilder UseContractsFromAssembly(this WebApplicationBuilder e, string assemblyName)
        {
            return HubconClientBuilder.Current.UseContractsFromAssembly(e, assemblyName);
        }
    }
}