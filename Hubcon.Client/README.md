Hubcon is a library that abstracts SignalR messages/returns using a custom interface, solving SignalR's hassle of implementing each message and method individually.

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

The project now uses JSON to provide strong type safety and complex object conversion.

# Usage

## Domain Project
After installing this package, create a two interfaces that implement IClientHubController and IServerHubController in your Domain project. 
Client and server MUST share this interface, or at least implement exactly the same interface on both sides.
You can choose to implement just one of them, this example just covers both cases.

    public interface IMyClientController : IClientController
    {
        Task<int> GetClientTemperature();
    }

    public interface IMyServerHub : IServerHubController
    {
        Task<int> GetServerTemperature();
    }

## Client
This project can be anything, a console application, a background worker or even a singleton service.

Create MyClientController, inherit ClientController, then implement IMyClientController. This will receive server calls and execute the requested methods.

    public class MyClientController(string hubUrl) : ClientController(hubUrl), IMyClientController
    {
        public async Task<int> GetTemperature() => await Task.Run(() => new Random().Next(-10, 50));
    }

You can inherit from ClientController like the last example, or use ClientController<> generic with an interface that implements IServerHubController to enable the Server property.
Server property allows executing server methods using the specified interface. This is optional.

    public class MyClientController(string url) : ClientController<IMyServerHub>(url), IMyClientController
    {
        public async Task<int> GetTemperature() => await Task.Run(() => new Random().Next(-10, 50));

        // Some example method
        private async Task CallServer()
        {
            // Some call to server
            Server.SomeServerMethod();
        }
    }

On your SignalR client's program.cs (in this case, an empty console application), you can now create it and Start it:

    static async Task Main()
    {
        var myClientController = new MyClientController("http://localhost:5001/myclienthub").StartServerInstance();
        var connector = myClientController.GetConnector<IMyServerHub>(); // Gets a "connector", which is used to call server methods.

        var temperature = connector.GetTemperatureFromServer();

        Console.WriteLine(temperature);

        Console.ReadKey();
    }

## Server
The server should be an ASP.NET Core 8 API, but should also work for Blazor and ASP.NET Core MVC.

Create ServerTestHubController.cs. This is the server's hub. It can implement methods and be called from clients.

    public class MyServerHub : ServerHub<IMyClientController>, IMyServerHub
    {
        // Returns some random temperature to client
        public async Task<int> GetTemperatureFromServer() => await Task.Run(() => new Random().Next(-10, 50));
    }

    // You can just inherit from ServerHub, or use ServerHub<T> to enable the Client variable, which refers to the current calling client.
    // Client variable allows executing calling client methods using the specified interface.


On your server program.cs's services:

    builder.Services
            .AddHubcon();
            .AddHubconClientAccessor(); // This is explained later

Same file, after builder.Build():

	app.MapHub<MyServerHub>("/myclienthub");
    
    // Just a test endpoint, it can also be injected in a controller.
    app.MapGet("/test", async (IClientAccessor<MyServerHub, IMyClientController> clientAccessor) =>
    {
        // Getting some connected clientId
        var clientId = clientAccessor.GetAllClients().First();

        // Gets a client instance
        var client = clientAccessor.GetClient(clientId);

        var temperature = await client.GetTemperature();

        return temperature.ToString();
    });


And that's it. Execute both projects at the same time and go to localhost:<port>/test, you should see the GetTemperature() method return a value.
You can experiment with any method.

## New Dependency Injection methods

AddHubcon() method registers all dependencies needed for hubcon to work properly.

    builder.Services
            .AddHubcon();

AddHubconClientAccessor() method allows injecting, like the name states, a client accessor. This means that, when you need interacting with a client from
some scoped class, you can do it through this interface.
This one is specially made for scoped contexts. Allows interacting with connected clients, for example, from a Blazor page, or an ASP.NET controller, like so:
NOTE: This is for server hubs.
You can change IClientAccessor generic to your ServerHub/IClientController combinations as you please, with no additional configuration.

    builder.Services
            .AddHubcon();
            .AddHubconClientAccessor();

After adding the client accessor, you can now inject it anywhere you need. For example:

        [ApiController]
        [Route("[controller]")]
        public class SomeAspNetController(IClientAccessor<MyServerHub, IMyClientController> clientAccessor) : ControllerBase
        {              
            [HttpGet]
            [Route("SomeMethod")]
            public async Task<int> SomeMethod()
            {
                // Using GetAllClients to get access to the current connected clients.
                var instances = clientAccessor.GetAllClients().First();

                // Using Getclient to retrieve a specific client, then executing a command.
                return await clientAccessor.GetClient(instances).GetTemperature();
            }
        }


AddHubconClientController() method registers and starts your ClientController as a Singleton, as only the server can access it.
This method also register IMyServerHub as a server instance, allowing you to call the server from anywhere you want.

    builder.Services
        .AddHubcon()
        .AddHubconClientController<MyClientController, IMyServerHub>("https://localhost:5001/myclienthub");

You can also use the dependency injection both methods provided at the same time, in case your project need to act both as a ServerHub 
and a Client of another server. Doesn't need a particular order after AddHubcon method.

    builder.Services
        .AddHubcon()
        .AddHubconClientAccessor()
        .AddHubconClientController<MyClientController, IMyServerHub>("https://localhost:5001/myclienthub");

## Adding more methods
To implement more methods, just add them to the interfaces, implement them in MyClientController, then use it somewhere from the server, it will just work.
This also applies on reverse, using IMyServerHub and clients.

## Use case
This fits perfectly if you need to communicate two instances in real time, in an easy and type-safe way. 
The wrappers are persisted in memory to avoid rebuilding overhead (will be further improved).

## Version changes
- Version updated to 1.7.0
- Simplified class names for better readability and understandability.
- Implemented IClientAccessor<THub,TIClientController> for easy client access.
- Refactored ClientController and fixed internal memory leaks.
- Added new dependency injection methods.
- Added scoped support for ClientController.
- Removed some unused classes and usings.
- Fixed memory leaks when creating the artificial client implementations.
- Updated readme

## Note
This project is under heavy development. The APIs might change.