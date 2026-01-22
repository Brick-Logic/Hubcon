using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Client.Builder;
using HubconTestClient.Contracts;
using System.Net.Http.Headers;

namespace HubconTestClient.Modules
{
    public sealed class OpenAIServerModule : RemoteServerModule
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
                x.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
            });

            // Contrato que va a usar esta config
            configuration.Implements<IOpenAIContract>();
        }
    }
}
