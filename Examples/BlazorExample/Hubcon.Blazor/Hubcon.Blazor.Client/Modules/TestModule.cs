using Hubcon.Blazor.Client.Auth;
using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Client.Builder;
using HubconTestDomain;

namespace Hubcon.Blazor.Client.Modules
{
    public class TestModule : RemoteServerModule
    {
        public override void Configure(IServerModuleConfiguration configuration)
        {
            // Url de base, sin protocolo
            configuration.WithBaseUrl("localhost:5000");

            // Agrego los contratos que este servidor implementa
            // Estos contratos se resuelven por DI con la configuracion puesta en este lugar
            configuration.Implements<IUserContract>();
            configuration.Implements<ISecondTestContract>();

            // Manager de autenticación (opcional)
            configuration.UseAuthenticationManager<AuthenticationManager>();

            // Usar conexion insegura
            configuration.UseInsecureConnection();
        }
    }
}
