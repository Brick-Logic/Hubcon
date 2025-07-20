using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Standard.Interfaces;
using Hubcon.Shared.Abstractions.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System.Net.WebSockets;

namespace Hubcon.Client.Builder
{
    internal class ServerModuleConfiguration(IClientBuilder builder, IServiceCollection services) : IServerModuleConfiguration
    {
        public IServerModuleConfiguration Implements<T>(Action<IContractConfigurator>? configure = null) where T : IControllerContract
        {
            if (typeof(T).IsClass || builder.Contracts.Any(x => x == typeof(T)))
                return this;

            LoadContractProxy(typeof(T));
            builder.Contracts.Add(typeof(T));

            builder.ConfigureContract<T>(configure);

            return this;
        }

        private void LoadContractProxy(Type contractType)
        {
            builder.LoadContractProxy(contractType, services);
        }

        public IServerModuleConfiguration UseAuthenticationManager<T>() where T : class, IAuthenticationManager
        {
            builder.UseAuthenticationManager<T>(services);
            return this;
        }

        public IServerModuleConfiguration WithBaseUrl(string hostUrl)
        {
            builder.BaseUri ??= new Uri(hostUrl);
            return this;
        }

        public IServerModuleConfiguration UseInsecureConnection()
        {
            builder.UseSecureConnection = false;
            return this;
        }

        public IServerModuleConfiguration ConfigureWebsockets(Action<ClientWebSocketOptions> options)
        {
            builder.WebSocketOptions ??= options;
            return this;
        }

        public IServerModuleConfiguration ConfigureHttpClient(Action<HttpClient> configure)
        {
            builder.HttpClientOptions ??= configure;
            return this;
        }

        public IServerModuleConfiguration WithHttpPrefix(string prefix)
        {
            builder.HttpPrefix ??= prefix;
            return this;
        }

        public IServerModuleConfiguration WithWebsocketEndpoint(string endpoint)
        {
            builder.WebsocketPrefix ??= endpoint;
            return this;
        }

        public IServerModuleConfiguration SetWebsocketPingInterval(TimeSpan timeSpan)
        {
            builder.WebsocketPingInterval ??= timeSpan;
            return this;
        }
    }
}