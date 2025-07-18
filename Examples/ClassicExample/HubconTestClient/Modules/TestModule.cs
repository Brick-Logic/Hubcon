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
            // Url de base, sin protocolo
            configuration.WithBaseUrl("localhost:5000");
            //configuration.WithPrefix("prefix1");
            //configuration.WithWebsocketEndpoint("wsprefix");

            // Agrego los contratos que este servidor implementa
            // Estos contratos se resuelven por DI con la configuracion puesta en este lugar
            configuration.Implements<IUserContract>(x => x.UseWebsocketMethods());
            configuration.Implements<ISecondTestContract>();

            // Manager de autenticación (opcional)
            configuration.UseAuthenticationManager<AuthenticationManager>();

            // Usar conexion insegura
            configuration.UseInsecureConnection();
        }
    }
}
