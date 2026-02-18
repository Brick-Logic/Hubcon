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

            server.Implements<IOpenAIContract>();
        }
    }
}
