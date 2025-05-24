using Hubcon.Client.Abstractions.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Hubcon.Client.Builder
{
    public class ClientBuilderRegistry(IProxyRegistry proxyRegistry) : IClientBuilderRegistry
    {
        private Dictionary<Type, IClientBuilder> ClientBuilders { get; } = new();

        public void RegisterModule<TRemoteServerModule>(IServiceCollection services) 
            where TRemoteServerModule : IRemoteServerModule, new()
        {
            var module = new TRemoteServerModule();

            var clientBuilder = new ClientBuilder(proxyRegistry);
            var builderConfig = new ServerModuleConfiguration(clientBuilder, services);
            module.Configure(builderConfig);

            foreach(var contractType in clientBuilder.Contracts)
            {
                ClientBuilders.Add(contractType, clientBuilder);

                services.AddSingleton(contractType, (serviceProvider) => {

                    var registry = serviceProvider.GetRequiredService<IClientBuilderRegistry>();

                    if (registry.GetClientBuilder(contractType, out IClientBuilder? value))
                    {
                        return value!.GetOrCreateClient(contractType, serviceProvider);
                    }

                    return default!;
                });
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
