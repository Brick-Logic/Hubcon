using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Standard.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System.Net.WebSockets;

namespace Hubcon.Client.Abstractions.Interfaces
{
    public interface IClientBuilder
    {
        Type? AuthenticationManagerType { get; set; }
        Uri? BaseUri { get; set; }
        List<Type> Contracts { get; set; }
        string? HttpPrefix { get; set; }
        bool UseSecureConnection { get; set; }
        string? WebsocketPrefix { get; set; }
        Action<ClientWebSocketOptions>? WebSocketOptions { get; set; }
        Action<HttpClient>? HttpClientOptions { get; set; }
        TimeSpan WebsocketPingInterval { get; set; }
        bool WebsocketRequiresPong { get; set; }
        int MessageProcessorsCount { get; set; }
        bool AutoReconnect { get; set; }
        bool ReconnectStreams { get; set; }
        bool ReconnectSubscriptions { get; set; }
        bool ReconnectIngests { get; set; }
        TimeSpan WebsocketTimeout { get; set; }
        TimeSpan HttpTimeout { get; set; }

        T GetOrCreateClient<T>(IServiceProvider services) where T : IControllerContract;
        object GetOrCreateClient(Type contractType, IServiceProvider services);
        void LoadContractProxy(Type contractType, IServiceCollection services);
        void UseAuthenticationManager<T>(IServiceCollection services) where T : class, IAuthenticationManager;
        void ConfigureContract<T>(Action<IContractConfigurator<T>>? configure) where T : IControllerContract;
    }
}