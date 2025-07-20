using Hubcon.Shared.Abstractions.Standard.Interfaces;
using Hubcon.Shared.Abstractions.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System.Net.WebSockets;

namespace Hubcon.Client.Abstractions.Interfaces
{
    public interface IServerModuleConfiguration
    {
        IServerModuleConfiguration Implements<T>(Action<IContractConfigurator>? configure = null) where T : IControllerContract;
        IServerModuleConfiguration UseAuthenticationManager<T>() where T : class, IAuthenticationManager;
        IServerModuleConfiguration WithBaseUrl(string baseUrl);
        IServerModuleConfiguration UseInsecureConnection();
        IServerModuleConfiguration WithHttpPrefix(string prefix);
        IServerModuleConfiguration WithWebsocketEndpoint(string endpoint);
        IServerModuleConfiguration ConfigureWebsockets(Action<ClientWebSocketOptions> options);
        IServerModuleConfiguration ConfigureHttpClient(Action<HttpClient> options);
        IServerModuleConfiguration SetWebsocketPingInterval(TimeSpan timeSpan);
    }
}