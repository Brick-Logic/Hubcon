using Autofac;
using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Shared.Components.Extensions;

namespace Hubcon.Client.Builder
{
    public class ClientBuilderRegistry(IProxyRegistry proxyRegistry) : IClientBuilderRegistry
    {
        private Dictionary<Type, IClientBuilder> ClientBuilders { get; } = new();

        public void RegisterModule<TRemoteServerModule>(List<Action<ContainerBuilder>> ServicesToInject) 
            where TRemoteServerModule : IRemoteServerModule, new()
        {
            var module = new TRemoteServerModule();

            var clientBuilder = new ClientBuilder(proxyRegistry);
            var builderConfig = new ServerModuleConfiguration(clientBuilder);
            module.Configure(builderConfig);

            foreach(var contractType in clientBuilder.Contracts)
            {
                ClientBuilders.Add(contractType, clientBuilder);
                ServicesToInject.Add(container => container
                    .RegisterWithInjector(x => x
                        .Register((context, b) =>
                        {
                            var registry = context.Resolve<IClientBuilderRegistry>();

                            if(registry.GetClientBuilder(contractType, out IClientBuilder? value))
                            {
                                return value!.GetOrCreateClient(contractType, context);
                            }

                            return default!;                             
                        })
                        .As(contractType)
                        .AsSingleton()));
            }
        }

        public bool GetClientBuilder(Type contractType, out IClientBuilder? value)
        {
            if (ClientBuilders.TryGetValue(contractType, out IClientBuilder? builder))
            {
                value = builder;
                return true;
            }

            value = null;
            return false;
        }
    }
}
