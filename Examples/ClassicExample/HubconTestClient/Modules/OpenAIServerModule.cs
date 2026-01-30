using HubconTestClient.Contracts;
using System.Net.Http.Headers;
using Hubcon;
using Microsoft.Extensions.Configuration;
using System;

namespace HubconTestClient.Modules
{
    public sealed class OpenAIServerModule : RemoteServerModule
    {
        private readonly IConfigurationRoot config;

        public OpenAIServerModule(IConfigurationRoot config)
        {
            this.config = config;
        }

        public override void Configure(IServerModuleConfiguration configuration)
        {
            configuration.WithBaseUrl("https://api.openai.com/");

            // Indica que el servidor no es hubcon
            configuration.NonHubconServer();

            //configuration.AddHeaderProvider("Authorization", services => services.Get...);

            Func<string> test = () => "a";

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
