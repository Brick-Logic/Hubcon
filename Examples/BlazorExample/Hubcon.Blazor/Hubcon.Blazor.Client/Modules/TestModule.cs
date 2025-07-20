using Hubcon.Blazor.Client.Auth;
using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Client.Builder;
using HubconTestDomain;

namespace Hubcon.Blazor.Client.Modules
{
    public class TestModule : RemoteServerModule
    {
        public override void Configure(IServerModuleConfiguration server)
        {
            // Url de base, sin protocolo
            server.WithBaseUrl("localhost:5000");

            // Agrego los contratos que este servidor implementa
            // Estos contratos se resuelven por DI con la configuracion puesta en este lugar

            server.Implements<IUserContract>(x => x.UseWebsocketMethods());

            server.Implements<ISecondTestContract>();

            // Manager de autenticación (opcional)
            server.UseAuthenticationManager<AuthenticationManager>();

            // Usar conexion insegura
            server.UseInsecureConnection();
        }
    }
}
