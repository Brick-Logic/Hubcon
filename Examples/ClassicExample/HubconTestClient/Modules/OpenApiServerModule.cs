using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Client.Builder;
using HubconTestClient.Contracts;
using System.Net.Http.Headers;

namespace HubconTestClient.Modules
{
    public sealed class OpenApiServerModule : RemoteServerModule
    {
        public override void Configure(IServerModuleConfiguration configuration)
        {
            configuration.WithBaseUrl("https://api.openai.com/");

            configuration.NonHubconServer();

            configuration.DisableHttpAuthentication();

            configuration.ConfigureHttpClient((x, y) =>
            {
                x.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
            });

            configuration.Implements<IOpenApiContract>();
        }
    }
}
