using Hubcon;
using HubconQuickStart.Shared;

var builder = WebApplication.CreateBuilder(args);

Console.Title = "HubconQuickStart.Server";

builder.Services.AddSwaggerGen();

// We add the hubcon server services
builder.AddHubconServer(x =>
{
    x.AutoRegisterControllers();
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

// Maps hubcon operations to HTTP and OpenAPI
app.UseHubconHttpEndpoints();

await app.RunAsync("http://localhost:5000");

internal class UserController(ILogger<UserController> logger) : IUserContract
{
    public async Task<string> TestHubcon(string message)
    {
        Console.Clear();
        logger.LogInformation($"Received message from client: {message}");
        logger.LogInformation($"Sending a response to the client.");
        return "Server response";
    }
}