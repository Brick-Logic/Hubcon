Hubcon is a library that abstracts SignalR messages/returns using interface methods, avoiding the hassle of implementing each message and method individually.

Creates an artificial implementation of your interface, and gives it everything it needs to communicate with a SignalR client seamlessly just with the hub URL. 

It currently supports:
- Async methods
- Sync methods (automatically wrapped as Async)
- Void methods
- Methods with and without return value
- Methods with and without parameters
- Nullable parameters
- Default parameters
- Variable parameters (params)
- IEnumerable
- Dictionary
- Classes in general

The project now uses MessagePack, greatly enhancing the type safety, stability and speed.

# Usage

## Domain Project
After installing this package, create a new interface that implements IHubController in your Domain project. 
Client and server MUST share this interface, or at least implement exactly the same interface on both sides.

    public interface ITestHubController : IHubController
    {
        Task<int> GetTemperature();
        Task ShowText();
    }

## Client
On your SignalR client, implement a concrete HubController with your created interface.
This project can be literally anything that spins up, even a console application.

    public class TestHubController(string url) : HubController(url), ITestHubController
    {
        public async Task ShowText() => Console.WriteLine("ShowText() invoked succesfully.");  
        public async Task<int> GetTemperature() => new Random().Next(-10, 50);
    }

On your SignalR client's program.cs, you can now create it and Start it:

    static async Task Main(string[] args)
    {
        var controller = new TestHubController("http://localhost:5001/clienthub"); // Use your port
    
        await controller.StartAsync(); // This is a blocking task
    }

## Server
The server should be an ASP.NET Core 8 API, but should also work for Blazor and ASP.NET Core MVC.

On your server program.cs's services:

    builder.Services.AddHubcon();
    builder.Services.AddScoped<ClientHubControllerConnector<ITestHubController, ServerTestHubController>>();

Same file, after builder.Build():

	app.MapHub<ServerTestHubController>("/clienthub");
    
    // Just a test endpoint, the connector can be injected via DI
	app.MapGet("/test", async (ClientHubControllerConnector<ITestHubController, ServerTestHubController> client) =>
    {
        // Getting some connected clientId
        var clientId = ServerHub<IServerTestHubController>.GetClients().FirstOrDefault()!.Id;

        // Gets a client instance
        var instance = client.GetInstance(clientId);

        // Using some methods
        await instance.ShowText();
        var temperature = await instance.GetTemperature();

        return temperature.ToString();
    });

And that's it. Execute both projects at the same time and go to localhost:<port>/test, you should see the ShowText() method print, and GetTemperature() return a value.

## Adding more methods
To implement more methods, just add them to the interface, implement them in the TestHubController, then use it somewhere from the server, it will just work.

## Use case
This fits perfectly if you need to communicate two instances in real time, in an easy and type-safe way. 
The wrappers are persisted in memory to avoid rebuilding overhead (will be further improved).

## Version changes
- Added Server Hub/Controller (same functionality, but adapted to Hubs).
- Implemented MessagePack for stronger type parsing and type safety through SignalR.
- Now you can reference the current Server target and caller Client in both controllers.
- Removed unused classes and old json parsers
- Greatly improved legibility and type safety by using Interfaces.
- Added new .AddHubcon() method for easy injection.
- Reorganized folders and namespaces for easier use.
- Fixed lots of warnings
- Fixed lots of comments
- Added proof of concept projects

## Note
This project is under heavy development. The APIs might change.