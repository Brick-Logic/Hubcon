# Hubcon

A high-performance, contract-based RPC framework for .NET that provides seamless communication over HTTP/WebSocket with interface-based usage, GraphQL-style subscriptions and real-time data streaming capabilities.

## 🚀 Key Features

- **Contract-Based Architecture**: Share interfaces between client and server - implement on server as controllers, use directly on client
- **Dual Transport Support**: HTTP and WebSocket with automatic fallback and reconnection
- **GraphQL-Style Subscriptions**: Bidirectional subscriptions (client-to-server and server-to-client)
- **Real-Time Data Ingestion**: Multiple simultaneous data streams using multiple `IAsyncEnumerable<T>` on a single call.
- **Advanced Subscription Model**: Dedicated `ISubscription<T>` interface for contract properties
- **Automatic Proxy Generation**: Source generators create client proxies automatically
- **Dependency Injection**: Full DI support for contracts on both client and server
- **Middleware Pipeline**: Compatible with ASP.NET Core middleware + custom middleware with DI, featuring an extended operation context.
- **Plug & Play**: Minimalistic configuration setup, extensive customization options.
- **High Performance**:                     
    - HTTP round-trip: Up to 66k RPS.
    - HTTP one-way call: Up to 70k RPS.
    - Websocket Round-Trip: Up to 80k RPS.
    - Websocket One-Way Call: Up to 140k RPS.
    - Websocket Ingest: 140k event/s.
    - Event Streaming and Subscriptions: Up to 450k events/s per receiver on client (scalable) .

    Tested on a Ryzen 5 5600X CPU.
    Single-threaded client (max 10% CPU).
    12 threads assigned to server.
    256 concurrent requests (TPL library). 
    HTTP consumes around 50% of the CPU, while WebSockets consume around 33% of the CPU.
    Observed stable ~45mb of RAM in all cases, both on client and server.

- **Rate Limiting**: Built-in throttling to prevent overload and ensure fair resource usage.
- **Optional remote cancellation**: Client-side tokens can optionally cancel local and remote operations using simple cancellation tokens.
- **Memory Optimized**: Made to sustain a very high throughput with minimal memory footprint. Leak-free, minimal alloc architecture.
- **OpenAPI Compatible**: Partial compatibility with OpenAPI specifications and partial automatic documentation.
- **Working examples**: This project includes a classic Client-Server example used as testbench and benchmark, a BlazorWasm-Server example and a triple microservice loop example.

## Features implementation state

✅ Complete – Implemented and functional.

⚠️ In progress – Currently being developed or partially functional.

🟡 Planned – Designed, pending implementation.

🔜 Coming – Planned for the next version.

### 🌐 HTTP – Features

| Feature                          | Description                                                                    | Status       |
|----------------------------------|--------------------------------------------------------------------------------|--------------|
| Invoke (Round Trip)              | RPC call with return value. Handles response coordination and exception propagation | ✅ Complete |
| Invoke with multiple parameters  | Supports serializable parameters + optional CancellationToken                  | ✅ Complete |
| TaskCompletionSource coordination | Auto-cleanup when Task is cancelled                                           | ✅ Complete |
| Remote exception propagation     | Server exceptions are thrown as `HubconRemoteException` on client             | ✅ Complete |
| Stress test (round-trip up to 66k RPS, 1 client)  | Stable under high concurrency                     | ✅ Complete |
| Fire and Forget (FaF)            | One-way call with no response or wait                                         | ✅ Complete |
| Exception handling in FaF        | Exceptions on server do not propagate to client                               | ✅ Complete |
| Throttling support               | Rate limiting to prevent overload                                             | ✅ Complete |
| Cancellation on disconnect       | FaF auto-cancels if connection or context is terminated                       | ✅ Complete |

### 📡 WebSocket – Features

| Feature                          | Description                                                                    | Status       |
|----------------------------------|--------------------------------------------------------------------------------|--------------|
| Stream (Server → Client)         | Stream data from server via `IAsyncEnumerable<T>`                              | ✅ Complete |
| Disconnection cancels stream     | Auto-cancel stream when connection is closed                                  | ✅ Complete |
| Idle timeout                     | Cleanup triggered on inactivity (no pull)                                     | ✅ Complete |
| Ingest (Client → Server)         | Push data from client via `IAsyncEnumerable<T>`                               | ✅ Complete |
| Heartbeat timeout                | Detects silent/lost connections automatically                                 | ✅ Complete |
| Error propagation in ingest      | Exceptions during ingest are sent to client                                   | ✅ Complete |
| Subscriptions (server → client)  | Push-based updates (observer style)                                           | ✅ Complete |
| Bounded channel for subs         | Prevents memory pressure under high load                                      | ✅ Complete |
| Manual unsubscription            | Client can unsubscribe explicitly via `ISubscription<T>`                      | ✅ Complete |
| Cleanup on disconnect            | All server-side handlers cleaned automatically                                | ✅ Complete |
| Server-only cancellation token   | Server operations receive `HubconContext.CancellationToken`                   | ✅ Complete |

### 🧩 Shared – Cross Transport Features

| Feature                                 | Description                                                                                      | Status           |
|-----------------------------------------|--------------------------------------------------------------------------------------------------|------------------|
| Source Generator (SG)                   | Auto-generates strongly-typed client proxies based on interfaces                                | ✅ Complete      |
| Ignores external CancellationToken      | Prevents serialization of external `CancellationToken` in contract methods                      | ✅ Complete      |
| Unified cancellation behavior            | Unified cancellation handling across all operations (stream, ingest, invoke, etc.)  | ✅ Complete  |
| WebSocket auto-reconnect (optional)    | Optional automatic reconnection when WebSocket connection drops                                 | ✅ Complete      |
| Configurable ping/pong                  | Ping/pong heartbeat configurable on client and server                                          | ✅ Complete      |
| Precise throttling mechanism            | New internal throttling system with per-operation granularity                                   | ✅ Complete      |
| Throttling configuration                | Throttling limits configurable globally, per contract, or per method                            | ✅ Complete      |
| Optional certificate support            | Supports client/server TLS certificates for HTTP and WebSocket                                 | ✅ Complete      |
| Dependency injection for RemoteModule   | `RemoteServerModule` supports transient registration for injected logic/configurations         | ✅ Complete      |
| Configuration via DI                    | Global, per-contract, per-handler, or per-method configuration                                  | ✅ Complete      |
| ASP.NET & custom middlewares            | Fully integrates with existing ASP.NET pipeline                                                 | ✅ Complete      |
| Analyzers                               | Detects sync methods, invalid return types, or bad patterns                                     | ✅ Complete      |
| Observability                           | Supports logging via `ILogger`; extensible to tracing/metrics (e.g., OpenTelemetry)            | ✅ Partial       |
| Semantic Versioning                     | Uses beta versions (`1.0.0-betaX`) with clear release goals                                     | ✅ Partial       |
| RC1 milestone                           | First stable RC will include improved cancellation and token coordination                       | ✅ Complete        |
| Operation multiplexing                  | All operations internally routed using `operationId` to enable full concurrency                 | ✅ Complete      |
| MCP Protocol                           | In progress: Protocol to connect AIs, supporting both WebSocket and HTTP transport               | ⚠️ In progress      |



## 📦 Installation

```bash
On client: dotnet add package Hubcon.Client
On server: dotnet add package Hubcon.Server
On your shared project: dotnet add package Hubcon.Shared
```

## 🏗️ Quick Start

For this, you need 3 projects:
1. A client project that will use the server, a console app is enough.
2. A server project ASP.NET Core Web API is recommended.
3. A shared project to define your contracts.

All projects must target .NET 8.0 or higher.

### 1. Define Your Contract
A contract is simply an interface that inherits from `IControllerContract`.
Put this in your shared project, which will be used by both client and server.

```csharp
public interface IUserContract : IControllerContract
{
    Task<User> GetUserAsync(int id);
}
```

### 2. Server Implementation

#### ⚪ Controller/ContractHandler implementation

Here you will implement your contract/interface, such as any normal class.
They behave similarly to ASP.NET Core traditional controllers.

```csharp
public class UserController: IUserContract
{
    public async Task<string> GetUserNameAsync(int id)
    {
        await Task.Delay(100); // Simulate some work
        Console.WriteLine($"User {id} requested.");
        return "HubconUser";
    }
}
```

Task or Task<T> usage is strongly recommended.

#### ⚪ Server-side program.cs

Before 'var app = builder.Build();'

```csharp
builder.AddHubconServer();
builder.ConfigureHubconServer(serverOptions =>
{
    serverOptions.AddController<UserController>();
});
```

After 'builder.Build();'

```csharp

// Maps all hubcon controllers to HTTP endpoints.
app.MapHubconControllers();

// This enables the hubcon websocket middleware. Not needed for now.
// app.UseHubconWebsockets();

```

These options can be used in any order and are fully independent.

### 3. Client Usage

#### ⚪ Creating a RemoteServerModule

A RemoteServerModule represents a server. It is used to describe a remote server, and implements
one or more contracts automatically based on it.

```csharp
internal class MyUserServerModule : RemoteServerModule
{
    public override void Configure(IServerModuleConfiguration server)
    {
        // Base url
        server.WithBaseUrl("localhost:5000");

        // Here you add your defined contracts. They will share the same configuration and websocket connection.
        server.Implements<IUserContract>();

        // You can switch to insecure connections. This includes 'https -> http' and 'wss to ws' protocols.
        server.UseInsecureConnection();
    }
}
```

Note: The 'Implements<T>' method will automatically generate a client proxy for the specified contract and proceed to register it in the DI container.
All contracts will point to the same server. If you need different servers, you can create multiple RemoteServerModules.

The only limitation is that you **cannot use the same contract on multiple RemoteServerModules**. If you do, hubcon will not allow it.

### ⚪ Register your RemoteServerModule

On your client-side program.cs...

```csharp
var builder = WebApplication.CreateBuilder();

builder.Services.AddHubconClient();
builder.Services.AddRemoteServerModule<MyUserServerModule>();

var app = builder.Build();
var scope = app.Services.CreateScope();

var client = scope.ServiceProvider.GetRequiredService<IUserContract>();

var result = await client.GetUserAsync(1);

Console.WriteLine($"User: {result}"); // Should print "User: HubconUser"
Console.ReadKey();
```

Congratulations! You made hubcon your first client-server call with hubcon.

Hubcon provides a lot of features and configurations, that will be explained in the next sections.

## Authentication and Authorization

### 🔐 Authentication manager
The authentication manager allows Hubcon to inject an authorization token on HTTP requests and
to authenticate the initial websocket connection.

```csharp
public class AuthenticationManager(ISecondTestContract secondTestContract) : BaseAuthenticationManager
{
    public override string? TokenType { get; protected set; } = "";
    public override string? AccessToken { get; protected set; } = "";
    public override string? RefreshToken { get; protected set; } = "";
    public override DateTime? AccessTokenExpiresAt { get; protected set; } = DateTime.UtcNow.AddYears(1);

    protected async override Task<IAuthResult> AuthenticateAsync(string username, string password)
    {
        var token = await secondTestContract.LoginAsync(username, password);

        TokenType = "Bearer";
        AccessToken = token;
        RefreshToken = "";
        AccessTokenExpiresAt = DateTime.UtcNow.AddYears(1);

        return AuthResult.Success(token, "", 100000);
    }

    protected override Task ClearSessionAsync()
    {
        TokenType = "";
        AccessToken = "";
        RefreshToken = "";
        AccessTokenExpiresAt = null;

        return Task.CompletedTask;
    }

    protected async override Task<PersistedSession?> LoadPersistedSessionAsync()
    {
        var token = await secondTestContract.LoginAsync("aaa", "bbb");

        TokenType = "Bearer";
        AccessToken = token;
        RefreshToken = "";
        AccessTokenExpiresAt = DateTime.UtcNow.AddYears(1);


        return new PersistedSession()
        {
            AccessToken = token,
            RefreshToken = ""
        };
    }

    protected async override Task<IAuthResult> RefreshSessionAsync(string refreshToken)
    {
        var token = await secondTestContract.LoginAsync(Username, Password);

        TokenType = "Bearer";
        AccessToken = token;
        RefreshToken = "";
        AccessTokenExpiresAt = DateTime.UtcNow.AddYears(1);

        return AuthResult.Success(token, "", 100000);
    }

    protected async override Task SaveSessionAsync()
    {
            
    }
}
```
All methods and subscriptions (including ISubscription<T> properties) allow the usage of the
[Authorize] attribute, including it's variants, and the [AllowAnonymous] attribute, for public access.

## 🔧 Advanced Features

### ⚪ Custom Middleware
Hubcon has it's own execution pipeline with custom middlewares, which come AFTER the ASP.NET's pipeline.
You can add global middlewares, and also controller-specific middlewares.

Lets define some basic middleware:
```csharp
public class LocalLoggingMiddleware(ILogger<LocalLoggingMiddleware> logger) : ILoggingMiddleware
{
    public async Task Execute(IOperationRequest request, IOperationContext context, PipelineDelegate next)
    {
        try
        {
            logger.LogInformation($"[Local] Operation {request.OperationName} started.");
            await next();
        }
        finally
        {
            logger.LogInformation($"[Local] Operation {request.OperationName} finished.");
        }
    }
}

public class GlobalLoggingMiddleware(ILogger<GlobalLoggingMiddleware> logger) : ILoggingMiddleware
{
    public async Task Execute(IOperationRequest request, IOperationContext context, PipelineDelegate next)
    {
        try
        {
            logger.LogInformation($"[Global] Operation {request.OperationName} started.");
            await next();
        }
        finally
        {
            logger.LogInformation($"[Global] Operation {request.OperationName} finished.");
        }
    }
}
```

Then we register their usage:
```csharp
// On server-side program.cs...
builder.ConfigureHubconServer(serverOptions =>
{
    // This will execute for ALL controllers.
    serverOptions.AddGlobalMiddleware<GlobalLoggingMiddleware>();

    serverOptions.AddController<UserController>(configure =>
    {
        // This will execute for this controller only.
        configure.AddMiddleware<LocalLoggingMiddleware>();

        // By default, local middlewares have priority, but you can use:
        x.UseGlobalMiddlewaresFirst();
    });
});
```

NOTE: There's a hard middleware order by type, which goes like this:

- ExceptionMiddleware (one local, one global)
- LoggingMiddlewares (multiple)
- AuthenticationMiddlewares (multiple)
- PreRequestMiddlewares (multiple)
- PreRequestMiddlewares(multiple)
- AuthorizationMiddlewares (multiple)
- GlobalRoutingMiddleware (internal middleware, cannot be changed)
- PostRequestMiddlewares (multiple)
- ResponseMiddlewares (multiple)

This option:
```csharp
    x.UseGlobalMiddlewaresFirst();
```

Will set global middlewares as priority in their own group.

Using that option, the global AuthorizationMiddleware will have priority over the local one, but will
still respect the type order.

### ⚪ Subscription Configuration
Subscriptions don't need configuration by default, they are plug and play, but you
can configure websocket-specific settings which affect or disable them:

```csharp
// Server-side program.cs...
builder.ConfigureHubconServer(serverOptions =>
{
    serverOptions.ConfigureCore(config => 
    {
        config
            .DisableWebSocketIngest()
            .DisableWebSocketMethods()
            .DisableWebsocketPing()
            .DisableWebSocketPong()
            .DisableWebSocketStream()
            .DisableWebSocketSubscriptions()
            .SetWebSocketTimeout(TimeSpan.FromSeconds(5));
    });
});
```

### ⚪ WebSocket Reconnection

The hubcon websocket client allows automatic reconnection without breaking existing subscriptions on the client
They will just wait for the websocket to reconnect and keep receiving messages.

This includes property subscriptions and streams (they will resend the request to restablish them), but will not recover Ingest Methods.

## 📊 Performance

Hubcon is designed for high-performance scenarios:

- **From 50k RPS on round trips up to 450k events per second on streaming** Both tested on a Ryzen 5 5600X CPU. Streaming was tested using a single receiver, which can be **horizontally scaled on server modules**.
- **Sub-millisecond latency** for local calls
- **Memory efficient** with zero-allocation hot paths and minimal memory footprint
- **Leak-free** architecture, tested on ultra high throughput scenarios.
- **Scalable** subscription management when returning IAsyncEnumerable<T>, allowing parameterized subscriptions.

## 🔌 Architecture

### Transport Layer
- **HTTP**: RESTful endpoints with JSON serialization with partial OpenAPI compatibility
- **WebSocket**: Real-time bidirectional communication using a lightweight messaging protocol.

### Contract System
- **Source Generation**: Automatic proxy generation at compile-time
- **Type Safety**: Full compile-time validation, any incompatible type will not be tolerated
- **Dependency Injection**: Seamless DI container integration (based on Autofac)

### Subscription Model
You can use ANY serializable data object, just return IAsyncCenumerable<MyType> or
use a ISubscription<T> property.

## 🤝 Integration

### ASP.NET Core Pipeline
Hubcon integrates seamlessly with the ASP.NET Core pipeline:
- Compatible with existing middleware (like Jwt middlewares)
- Supports authentication and authorization
- Works with model binding and validation
- Integrates with logging and metrics through middlewares.

### Dependency Injection
Just inject the contract you need, and hubcon will magically do the rest.

## 📝 Requirements

- **.NET 8.0** or higher
- **ASP.NET Core 8.0** or higher (server-side)

## 🏆 Why Hubcon?

- **Developer Experience**: Write once, use everywhere, never think about transport again
- **Performance**: Optimized for high-throughput scenarios
- **Flexibility**: HTTP or WebSocket, your contract, your choice
- **Real-time**: Built-in subscription and streaming support
- **Maintainable**: Strong typing and compile-time validation
- **Scalable**: Efficient resource management and connection pooling

## Where did Hubcon come from?

Hubcon started as controllers for SignalR. I hated having to register every message manually, 
so i made a custom abstract Hub class, which meant controllers for both server and client. 
Both would implement controllers and their contracts.

It was good but... **It was simply not enough**.

I explored GraphQL (HotChocolate) as a transport layer for hubcon, and it worked great, until i saw
how limiting it is for clients and general capabilities. Not to mention how hard it was to simply configure a little, **just a little** 
more custom solution. Not to mention it **always broke** the IObservable<T>'s it generated for subscriptions, making everything **harder to implement**.

So i made my own websocket messaging protocol. 

Implemented a better subscription system which doesn't break when the connection is lost,
it just waits for the reconnection and re-subscribes, and everything works as always.

Implemented an ingest system. Servers can receive one or multiple IAsyncEnumerable<T>'s from the client and consume them in multiple tasks.

Implemented a seamless method calling system, through HTTP or Websockets, as you wish.

Implemented a lightweight custom middleware pipeline with extended details about the operation, and the parsed request.

Just because **i hate manual integrations**.

## Project status
This project in a mature beta state, and it will soon be used in real cross-platform projects to show its capabilities.

## 📄 License

This project is licensed, for now, under a Personal Use License - see the [LICENSE](LICENSE) file for details.
This will change in the future when the first stable version is out.

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## 📞 Support

For questions and support, please open an issue on GitHub.
