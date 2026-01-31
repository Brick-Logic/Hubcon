using Hubcon.Blazor.Client.Auth;
using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Client.Builder;
using Hubcon.Client.Integration.Client;
using Hubcon.Shared.Core.Websockets.Interfaces;
using HubconTestDomain;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using System.Net.Http.Headers;

namespace Hubcon.Blazor.Client.Modules
{
    public class TestModule : RemoteServerModule
    {
        public override void Configure(IServerModuleConfiguration server)
        {
            // Url de base, sin protocolo
            server.WithBaseUrl("localhost:5000");

            server.EnableLogging();

            // Agrego los contratos que este servidor implementa
            // Estos contratos se resuelven por DI con la configuracion puesta en este lugar

            server.Implements<IUserContract>((x =>
            {
                x.SetDefaultTransport<WebSocketTransport>();

                x.ConfigureOperations((selector =>
                {
                    selector
                        .Configure(contract => contract.GetMessages(default(int)))
                        .UseTransport<HttpTransport>();
                }));
            }));

            server.Implements<ISecondTestContract>();

            // Manager de autenticación (opcional)
            server.UseAuthenticationManager<AuthenticationManager>();

            // Usar conexion insegura
            server.UseInsecureConnection();
        }
    }
}
