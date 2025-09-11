using ExampleMicroservicesDomain;
using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Client.Builder;

namespace ExampleMicroservice1.ServerModules
{
    internal class Microservice2ServerModule : RemoteServerModule
    {
        public override void Configure(IServerModuleConfiguration configuration)
        {
            // Url de base, sin protocolo
            configuration.WithBaseUrl("localhost:5002");

            // Agrego los contratos que este servidor implementa
            // Estos contratos se resuelven por DI con la configuracion puesta en este lugar
            configuration.Implements<IExampleMicroservice2Contract>();

            configuration.DisableHttpAuthentication();

            // Usar conexion insegura
            configuration.UseInsecureConnection();
        }
    }
}
