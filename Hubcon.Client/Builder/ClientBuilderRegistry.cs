using Hubcon.Client.Abstractions.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace Hubcon.Client.Builder
{
    public class ClientBuilderRegistry : IClientBuilderRegistry
    {
        private readonly IProxyRegistry _proxyRegistry;

        private readonly ConcurrentDictionary<Type, IClientBuilder> _clientBuilders = new();

        public ClientBuilderRegistry(IProxyRegistry proxyRegistry)
        {
            _proxyRegistry = proxyRegistry ?? throw new ArgumentNullException(nameof(proxyRegistry));
        }

        public void RegisterModule<TRemoteServerModule>(IServiceCollection services)
            where TRemoteServerModule : IRemoteServerModule, new()
        {
            var module = new TRemoteServerModule();

            var clientBuilder = new ClientBuilder(_proxyRegistry);
            var builderConfig = new ServerModuleConfiguration(clientBuilder, services);
            module.Configure(builderConfig);

            foreach (var contractType in clientBuilder.Contracts)
            {
                // TryAdd para evitar excepciones en caso de contratos repetidos
                _clientBuilders.TryAdd(contractType, clientBuilder);

                // Capturar localmente contractType para el closure
                var localContractType = contractType;

                services.AddSingleton(localContractType, serviceProvider =>
                {
                    var registry = serviceProvider.GetRequiredService<IClientBuilderRegistry>();

                    if (registry.GetClientBuilder(localContractType, out var builder))
                    {
                        return builder!.GetOrCreateClient(localContractType, serviceProvider);
                    }

                    return default!;
                });
            }
        }

        public bool GetClientBuilder(Type contractType, out IClientBuilder? value)
        {
            if (_clientBuilders.TryGetValue(contractType, out var builder))
            {
                value = builder;
                return true;
            }

            value = null;
            return false;
        }
    }

}
