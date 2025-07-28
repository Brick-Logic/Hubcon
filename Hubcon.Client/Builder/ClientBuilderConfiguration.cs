using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Standard.Interfaces;
using Hubcon.Shared.Abstractions.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System.Net.WebSockets;

namespace Hubcon.Client.Builder
{
    internal class ServerModuleConfiguration(IClientBuilder builder, IServiceCollection services) : IServerModuleConfiguration
    {
        public IServerModuleConfiguration Implements<T>(Action<IContractConfigurator<T>>? configure = null) where T : IControllerContract
        {
            if (typeof(T).IsClass || builder.Contracts.Any(x => x == typeof(T)))
                return this;

            LoadContractProxy(typeof(T));
            builder.Contracts.Add(typeof(T));
            builder.ConfigureContract(configure);

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

        public IServerModuleConfiguration ConfigureWebsocketClient(Action<ClientWebSocketOptions> options)
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
            builder.WebsocketPingInterval = timeSpan;
            return this;
        }

        public IServerModuleConfiguration RequirePongResponse(bool value = true)
        {
            builder.WebsocketRequiresPong = value;
            return this;
        }

        public IServerModuleConfiguration ScaleMessageProcessors(int count = 1)
        {
            builder.MessageProcessorsCount = count;
            return this;
        }

        public IServerModuleConfiguration EnableWebsocketAutoReconnect(bool value = true)
        {
            builder.AutoReconnect = value;
            return this;
        }

        public IServerModuleConfiguration ResubcribeStreamingOnReconnect(bool value = true)
        {
            builder.ReconnectStreams = value;
            return this;
        }

        public IServerModuleConfiguration ResubscribeOnReconnect(bool value = true)
        {
            builder.ReconnectSubscriptions = value;
            return this;
        }

        public IServerModuleConfiguration ResubscribeIngestOnReconnect(bool value = true)
        {
            builder.ReconnectIngests = value;
            return this;
        }

        public IServerModuleConfiguration SetWebsocketTimeout(TimeSpan timeSpan)
        {
            builder.WebsocketTimeout = timeSpan;
            return this;
        }

        public IServerModuleConfiguration SetHttpTimeout(TimeSpan timeSpan)
        {
            builder.HttpTimeout = timeSpan;
            return this;
        }
    }
}