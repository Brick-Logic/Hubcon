# Hubcon

Use C# interfaces as multi-transport API contracts. _Create an interface, implement on the server, use it on the client._

Hubcon is a high-performance, near zero-allocation RPC framework for .NET. It enables developers to implement strongly-typed, fast communications via HTTP, WebSockets or **Custom Transports** by simply sharing interfaces.
## 🚀 Key Features
- **Contract-Based**: Share interfaces between client and server as the single source of truth.
- **Transport Agnostic**: Native support for HTTP and WebSockets, compatible with standard REST integrations and OpenAPI.
- **High Performance:** Optimized for high throughput and low latency.
- **Memory Optimized**: Zero-allocation hot paths and minimal memory footprint.
- **Native AOT**: Full support for ahead-of-time compilation for cloud-native apps.
- **Streaming & Ingest**: Built-in support for bidirectional `IAsyncEnumerable<T>` streams.

## 📊 Performance

| Operation Type                      |   Requests per second   |
|-------------------------------------|-------------------------|
| HTTP round-trip - 48 tasks*         |         ~60.000         |
| HTTP one-way call - 48 tasks*       |         ~72.000         |
| WebSocket Round-Trip - 256 tasks*   |         ~85.000         |
| WebSocket One-Way Call - 256 tasks* |         ~110.000        |
| WebSocket Ingest 256 tasks*         |         ~110.000        |
| WebSocket Event Streaming           |         ~600.000        |

### Some notes:
- Tests include Jwt Authentication and Authorization, and multiple framework middlewares.
- Client and server shared hardware (_Tested on Ryzen 5 5600X CPU_)
- *Number of tasks that share the same Hubcon client.
- Client used a single physical core and a single WebSocket connection for this test.

For a far more detailed explanation about the tests like memory consumption, CPU usages, middlewares used, garbage collector pressure and more, please check the [Wiki Benchmarks Page](https://github.com/Brick-Logic/Hubcon/wiki/Benchmarks).

## 🏗️ Quick Start
This quick start is designed to take around 3 minutes to setup.

### Prerequisites
For this, we need to create 3 projects:

![projects.jpg](ReadmeImages/projects.jpg)

1. A client project, we are using a console app for this example. Requires .NET 5+.
2. A server project, we are using ASP.NET Core Web API for this example. Requires .NET 8+.
3. A shared project to define your contracts.

We will be using NET 8 as a target in all 3 projects for this example and top-level statements for simplicity.

We will explain the important components later.

### 1. Installation
Next, we install the `Hubcon` nuget package on all projects.

```
dotnet add package Hubcon --version 2.0.0-rc1
```

Or use the Nuget package manager with the "Include prerelease" option enabled. 

You can also install `Hubcon.Client`, `Hubcon.Server` and `Hubcon.Shared` individually, that's up to you.

### 2. 📜 Define Your Contract

A contract is any C# interface that inherits from `IControllerContract`.
Put this interface in your shared project, which will be referenced by both client and server.

```csharp
using Hubcon;

[HttpTransport]
public interface IUserContract : IControllerContract
{
    Task<string> TestHubcon(string message);
}
```

### 3. 💻 Client configuration
In program.cs, copy and paste this code:
```csharp
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
var myService = app.Services.GetRequiredService<MyService>();
await myService.DoSomething();

// An example service injecting both the contract and a logger through dependency injection
internal class MyService(IUserContract userContract, ILogger<MyService> logger)
{
    public async Task DoSomething()
    {
        logger.LogInformation("Sending message to the server...");
        // Use the contract through the Execute() method
        var response = await userContract.Execute(contract => contract.TestHubcon("Message from client"));
        logger.LogInformation($"Received from server: {response.Data}");
        Console.ReadKey();
    }
}

// Hubcon's configuration class. Represents a server. Multiple modules can be defined.
internal class TestRemoteServerModule : RemoteServerModule
{
    public override void Configure(IServerModuleConfiguration server)
    {
        server.WithBaseUrl("localhost:5000");
        server.UseInsecureConnection(); // For testing purposes only

        // The module implements a contract.
        // A contract can only be implemented by one module.
        server.Implements<IUserContract>();

        // Modules can implement any contract counts.
        // server.Implements<IAuthContract>();
        // server.Implements<IProductsContract>();
        // server.Implements<ICategoriesContract>();
        // ...
    }
}
```

### 4. 🌐 Server configuration
In program.cs, copy and paste this code:
```csharp
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
```

Congratulations! By running both projects you easily called a Hubcon HTTP endpoint from a client, using your own interface.

### Some notes:
- You can also access http://localhost:5000/swagger to check the mapped method and any methods you add.
- The `Execute` method automatically catches errors and provides a more complete and robust response without performance impacts. You can use the interface method directly, but this method is recommended.

The Quick Start solution is available in the `Examples` folder for you to use.

## 📖 Documentation & Advanced Usage
The full technical reference is available in our [GitHub Wiki](https://github.com/Brick-Logic/Hubcon/wiki). Check it out for:

- **Advanced Configuration**: Setting up RemoteServerModule and BaseAuthenticationManager.
- **Middleware Pipeline**: How to create custom global or per-endpoint middlewares.
- **Custom Transports**: Extending Hubcon with your own protocol layers.
- **Rate Limiting**: Detailed client and server-side self-preservation settings.
- **Non-Hubcon REST**: Integrating with external APIs (like OpenAI) using interfaces.

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## 📞 Support

For questions and support, please open an issue on GitHub.

## Contact

For direct contact, you can email **brick.logic.contact@gmail.com**.

## 📄 License

The current [Personal Use License](LICENSE) is temporary. A more flexible hybrid license (supporting both open-source and commercial use) will be introduced with the stable v2.0.0 release.

Developed with ❤️ to end the era of manual and repetitive API integrations.
