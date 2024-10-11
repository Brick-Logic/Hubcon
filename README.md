Hubcon is a library that abstracts SignalR messages/returns using interface methods, avoiding the hassle of implementing each message and method individually.

Creates an artificial implementation of your interface, and gives it everything it needs to communicate with a SignalR client seamlessly just with the hub URL. 

I only tested async Task methods with primitive types for now, but it should work with more complex types.

# Usage

## Domain Project
After installing this package, create a new interface that implements IHubController in your Domain project. 
Client and server MUST share this interface, or at least implement exactly the same interface.

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
        public async Task ShowText() => Console.WriteLine("Hello.");
        public async Task<int> GetTemperature() => new Random().Next(-10, 50);
    }

On your SignalR client's program.cs, you can now create it and Start it:

    static async Task Main(string[] args)
    {
        var controller = new TestHubController(url);
        await controller.Connection.StartAsync();
        Console.ReadLine();
    }

## Server
The server should be an ASP.NET Core 8 API, but should also work for Blazor and ASP.NET Core MVC.

On your server, create a ClientHub:

	using Microsoft.AspNetCore.SignalR;

	public record class ClientReference(string Id, string Name);
	public class ClientHub : Hub
	{
		public static event EventHandler ClientsChanged;
		public static Dictionary<string, ClientReference> Clients = new();
		public IHubContext<ClientHub> Hub { get; set; }
		public ClientHub(IHubContext<ClientHub> hub)
		{
			Hub = hub;
		}

		public override Task OnConnectedAsync()
		{
			Clients.TryAdd(Context.ConnectionId, new ClientReference(Context.ConnectionId, "SomeName"));
			return base.OnConnectedAsync();
		}

		public override Task OnDisconnectedAsync(Exception exception)
		{
			Clients.Remove(Context.ConnectionId, out _);
			return base.OnDisconnectedAsync(exception);
		}
	}

On your server program.cs's services:

    builder.Services.AddSignalR();
    builder.Services.AddControllers();

Same file, after builder.Build():

	app.MapControllers();
	app.MapHub<ClientHub>("/clienthub");
	app.Map("/test", async (IHubContext<ClientHub> hub) =>
	{
		var clientId = ClientHub.Clients.FirstOrDefault().Key;
		var client = new HubControllerClientBuilder<ITestHubController>(hub).GetClient(clientId);
		await client.ShowText();
		var temperature = await client.GetTemperature();
		Console.WriteLine(temperature);
	});

And you are done. Execute both projects at the same time and go to localhost:{port}/test, you should see the the values print on both screens.

To implement more methods, just add them to the interface, implement them in the TestHubController, then use it somewhere from the server, it will just work, 
specially if injected from a DI container.

As long as you have the clientId, you can create and use this client from anywhere in the server.

This fits perfectly if you need to communicate two instances in real time, in an easy way. The created clients are persisted in memory to avoid rebuilding.

In future versions, i'll add reacting to events and other functionality.


## Note
This is a very early stage project, it is not in good shape for production. Use it at your own risk.