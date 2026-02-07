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

        public override void Configure(IServerModuleConfiguration server)
        {
            server.WithBaseUrl("https://api.openai.com/");

            server.UseNonHubconHttp();

            server.AddHeaderProvider("Authorization", x => "Bearer " + config["OpenAI:ApiKey"]);

            //server.ConfigureHttpClient((x, y) =>
            //{
            //    // Autenticacion manual
            //    var key = config["OpenAI:ApiKey"];
            //    x.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
            //});

            // Contrato que va a usar esta config
            server.Implements<IOpenAIContract>();
        }
    }
}
