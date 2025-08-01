using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Client.Builder;
using Hubcon.Shared.Abstractions.Enums;
using HubconTestClient.Auth;
using HubconTestDomain;

namespace HubconTestClient.Modules
{
    internal class TestModule : RemoteServerModule
    {
        public override void Configure(IServerModuleConfiguration configuration)
        {
            configuration.WithBaseUrl("localhost:5000");

            configuration.EnableWebsocketAutoReconnect(true);

            configuration.Implements<IUserContract>(x =>
            {
                x.UseWebsocketMethods();

                x.ConfigureOperations(operationSelector =>
                {
                    operationSelector.Configure(contract => contract.GetTemperatureFromServer)
                        .UseTransport(TransportType.Websockets);

                    operationSelector.Configure(contract => contract.CreateUser)
                        .UseTransport(TransportType.Http);
                });
            });

            configuration.Implements<ISecondTestContract>();

            configuration.ConfigureWebsocketClient(x =>
            {
                x.SetBuffer(4 * 1024, 4 * 1024);
            });

            configuration.SetWebsocketPingInterval(TimeSpan.FromSeconds(1));
            configuration.RequirePongResponse(true);
            configuration.ScaleMessageProcessors(2);

            configuration.ConfigureHttpClient(x =>
            {
                x.Timeout = TimeSpan.FromSeconds(15);
                x.DefaultRequestHeaders.Add("User-Agent", "HubconTestClient");
            });

            // Manager de autenticación (opcional)
            configuration.UseAuthenticationManager<AuthenticationManager>();

            // Usar conexion insegura
            configuration.UseInsecureConnection();
        }
    }
}