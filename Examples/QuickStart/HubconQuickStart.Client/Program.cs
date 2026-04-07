using Hubcon;
using HubconQuickStart.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

Console.Title = "HubconQuickStart.Client";

// We add the hubcon client services
builder.Services.AddHubconClient();

// We register the remote server module
builder.Services.AddRemoteServerModule<TestRemoteServerModule>();

// We add some testing service
builder.Services.AddSingleton<MyService>();

var app = builder.Build();

// We simulate a service being used
var userContract = app.Services.GetRequiredService<MyService>();
await userContract.DoSomething();

internal class MyService(IUserContract userContract, ILogger<MyService> logger)
{
    public async Task DoSomething()
    {
        logger.LogInformation("Sending message to the server...");
        var response = await userContract.Execute(contract => contract.TestHubcon("Message from client"));
        logger.LogInformation($"Received from server: {response.Data}");
        Console.ReadKey();
    }
}

internal class TestRemoteServerModule : RemoteServerModule
{
    public override void Configure(IServerModuleConfiguration server)
    {
        server.WithBaseUrl("localhost:5000");
        server.UseInsecureConnection(); // For testing purposes only

        server.Implements<IUserContract>();
    }
}