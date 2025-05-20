using Autofac;
using Hubcon.Core.Abstractions.Interfaces;
using Hubcon.Core.Extensions;

namespace Hubcon.Client
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
                            
                            var scope = context.Resolve<ILifetimeScope>();
                            var registry = context.Resolve<IClientBuilderRegistry>();

                            if(registry.GetClientBuilder(contractType, out IClientBuilder? value))
                            {
                                return value!.GetOrCreateClient(contractType, scope);
                            }

                            return default!;      
                            
                        }).As(contractType)));
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
