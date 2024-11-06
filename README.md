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

The project now uses MessagePack, greatly enhancing speed and bandwidth usage, and JSON to provide strong type safety and complex object conversion.

# Usage

## Domain Project
After installing this package, create a two interfaces that implement IClientHubController and IServerHubController in your Domain project. 
Client and server MUST share this interface, or at least implement exactly the same interface on both sides.
You can implement one of them if you only need to control the client or the server.

    public interface ITestHubController : IClientHubController
    {
        Task<int> GetTemperature();
        Task ShowText();
        Task Random();
    }

    public interface IServerTestHubController : IServerHubController
    {
        Task<int> GetTemperatureFromServer();
        Task ShowTextOnServer();
        Task ShowTempOnServerFromClient();
    }

## Client
On your SignalR client, create a TestHubController that implements your interface (ITestHubController)
This project can be anything, a console application, a background worker or even a singleton service.

    public class TestHubController(string url) : ClientHubController<IServerTestHubController>(url), ITestHubController
    {
        public async Task ShowText() => await Task.Run(() => Console.WriteLine("ShowText() invoked succesfully."));
        public async Task<int> GetTemperature() => await Task.Run(() => new Random().Next(-10, 50));

        public async Task Random()
        {
            var temperatura = await Server.GetTemperatureFromServer();
            Console.WriteLine($"Temperatura desde el conector: {temperatura}");
        }
    }

    // You can inherit from ClientHubController, or use ClientHubController\<T\> to enable the Server variable, which refers to the current connected ServerHub.
    // Server variable allows executing server methods using the specified interface.

On your SignalR client's program.cs, you can now create it and Start it:

    static async Task Main()
    {
        var connector = new TestHubController("http://localhost:5001/clienthub")
            .GetConnector<IServerTestHubController>(); // Gets a "connector", which is a server client.

        // Executes a method on server
        await connector.ShowTextOnServer();

        // Gets data from server
        var serverData = await connector.GetTemperatureFromServer();
        Console.WriteLine(serverData);

        // Executes something on server, server asks for data to client, then prints the result
        await connector.ShowTempOnServerFromClient();

        Console.ReadKey();
    }

## Server
The server should be an ASP.NET Core 8 API, but should also work for Blazor and ASP.NET Core MVC.

On your server program.cs's services:

    builder.Services.AddHubcon();
    builder.Services.AddScoped<ClientHubControllerConnector<ITestHubController, ServerTestHubController>>();

Same file, after builder.Build():

	app.MapHub<ServerTestHubController>("/clienthub");
    
    // Just a test endpoint, it can also be injected in a controller.
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

Create ServerTestHubController.cs. This is the server's hub. It can implement methods and be called from clients.

    public class ServerTestHubController : ServerHub<ITestHubController>, IServerTestHubController
    {
        // Returns some random temperature to client
        public async Task<int> GetTemperatureFromServer() => await Task.Run(() => new Random().Next(-10, 50));

        // Just prints some text when called from client
        public async Task ShowTextOnServer() => await Task.Run(() => Console.WriteLine("ShowTextOnServer() invoked succesfully."));

        // This will be called from client, then this method gets temperature from the client.
        public async Task ShowTempOnServerFromClient() => Console.WriteLine($"ShowTempOnServerFromClient: {await Client.GetTemperature()}");
    }

    // You can just inherit from ServerHub, or use ServerHub\<T\> to enable the Client variable, which refers to the current calling client.
    // Client variable allows executing calling client methods using the specified interface.


And that's it. Execute both projects at the same time and go to localhost:<port>/test, you should see the ShowText() method print, and GetTemperature() return a value.


## Adding more methods
To implement more methods, just add them to the interface, implement them in the TestHubController, then use it somewhere from the server, it will just work.
This also ServerTestHubController and clients.

## Use case
This fits perfectly if you need to communicate two instances in real time, in an easy and type-safe way. 
The wrappers are persisted in memory to avoid rebuilding overhead (will be further improved).

## Version changes
- Organized project namespaces for simpler implementation
- Updated usage documentation
- Removed some unused classes
- Updated test projects

## Note
This project is under heavy development. The APIs might change.