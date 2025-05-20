using Hubcon.Client;
using Hubcon.Core.Abstractions.Interfaces;
using Hubcon.Core.Authentication;
using HubconTestDomain;

namespace HubconTestClient.Modules
{
    internal class TestModule : RemoteServerModule
    {
        public override void Configure(IServerModuleConfiguration configuration)
        {
            // Url de base, sin protocolo
            configuration.WithBaseUrl("localhost:5000");

            // Agrego los contratos que este servidor implementa
            // Estos contratos se resuelven por DI con la configuracion puesta en este lugar
            configuration.AddContract<ITestContract>();
            configuration.AddContract<ISecondTestContract>();

            // Manager de autenticación (opcional)
            //configuration.UseAuthenticationManager<BaseAuthenticationManager>();

            // Usar conexion insegura
            configuration.UseInsecureConnection();
        }
    }
}
