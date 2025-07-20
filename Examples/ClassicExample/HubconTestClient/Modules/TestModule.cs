using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Client.Builder;
using HubconTestClient.Auth;
using HubconTestDomain;

namespace HubconTestClient.Modules
{
    internal class TestModule : RemoteServerModule
    {
        public override void Configure(IServerModuleConfiguration configuration)
        {
            configuration.WithBaseUrl("localhost:5000");

            configuration.Implements<IUserContract>();
            configuration.Implements<ISecondTestContract>();

            configuration.ConfigureWebsockets(x =>
            {
                x.SetBuffer(1024 * 1024, 1024 * 1024);
                x.KeepAliveInterval = TimeSpan.FromSeconds(30);
            });

            configuration.SetWebsocketPingInterval(TimeSpan.FromSeconds(30));

            configuration.ConfigureHttpClient(x =>
            {
                x.Timeout = TimeSpan.FromSeconds(30);
                x.DefaultRequestHeaders.Add("User-Agent", "HubconTestClient");
            });

            // Manager de autenticación (opcional)
            configuration.UseAuthenticationManager<AuthenticationManager>();

            // Usar conexion insegura
            configuration.UseInsecureConnection();
        }
    }
}
