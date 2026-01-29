using HubconTestClient.Contracts;
using System.Net.Http.Headers;
using Hubcon;

namespace HubconTestClient.Modules
{
    public sealed class OpenAIServerModule(IConfigurationRoot config) : RemoteServerModule
    {
        public override void Configure(IServerModuleConfiguration configuration)
        {
            configuration.WithBaseUrl("https://api.openai.com/");

            // Indica que el servidor no es hubcon
            configuration.NonHubconServer();

            // Evita el uso de AuthenticationManager
            configuration.DisableHttpAuthentication();

            configuration.ConfigureHttpClient((x, y) =>
            {
                // Autenticacion manual
                var key = config["OpenAI:ApiKey"];
                x.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
            });

            // Contrato que va a usar esta config
            configuration.Implements<IOpenAIContract>();
        }
    }
}
