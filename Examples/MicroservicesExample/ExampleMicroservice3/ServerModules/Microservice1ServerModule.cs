using ExampleMicroservicesDomain;
using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Client.Builder;

namespace ExampleMicroservice3.ServerModules
{
    internal class Microservice1ServerModule : RemoteServerModule
    {
        public override void Configure(IServerModuleConfiguration configuration)
        {
            // Url de base, sin protocolo
            configuration.WithBaseUrl("localhost:5001");

            // Agrego los contratos que este servidor implementa
            // Estos contratos se resuelven por DI con la configuracion puesta en este lugar
            configuration.Implements<IExampleMicroservice1Contract>();

            configuration.DisableHttpAuthentication();

            // Usar conexion insegura
            configuration.UseInsecureConnection();
        }
    }
}
