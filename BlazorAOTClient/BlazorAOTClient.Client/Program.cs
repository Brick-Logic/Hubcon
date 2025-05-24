using BlazorAOTClient.Client.Auth;
using BlazorAOTClient.Client.Modules;
using Hubcon.Client;
using HubconTestDomain;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using System.Diagnostics;
using static System.Formats.Asn1.AsnWriter;

namespace BlazorAOTClient.Client
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.Services.AddHubconClient();
            builder.Services.AddRemoteServerModule<TestModule>();

            var app = builder.Build();
            await app.RunAsync();
        }
    }
}
