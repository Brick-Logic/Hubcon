using Hubcon.Blazor.Client.Auth;
using Hubcon.Blazor.Client.Modules;
using Hubcon.Client;
using Hubcon.Shared.Abstractions.Interfaces;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

namespace Hubcon.Blazor.Client
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);

            builder.Services.AddLogging();
            
            builder.Services.AddHubconClient();
            builder.Services.AddRemoteServerModule<TestModule>();
            builder.Services.AddSingleton<HttpClient, HubconHttpClient>();


            var app = builder.Build();

            await app.RunAsync();
        }
    }
}
