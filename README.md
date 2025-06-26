# Hubcon

A high-performance, contract-based RPC micro-framework for .NET that provides seamless communication over HTTP/WebSocket with GraphQL-style subscriptions and real-time data streaming capabilities.

## 🚀 Key Features

- **Contract-Based Architecture**: Share interfaces between client and server - implement on server as controllers, use directly on client
- **Dual Transport Support**: HTTP and WebSocket with automatic fallback and reconnection
- **GraphQL-Style Subscriptions**: Bidirectional subscriptions (client-to-server and server-to-client)
- **Real-Time Data Ingestion**: Multiple simultaneous data streams using multiple `IAsyncEnumerable<T>` on a single call.
- **Advanced Subscription Model**: Dedicated `ISubscription<T>` interface for contract properties
- **Automatic Proxy Generation**: Source generators create client proxies automatically
- **Dependency Injection**: Full DI support for contracts on both client and server
- **Middleware Pipeline**: Compatible with ASP.NET Core middleware + custom middleware with DI, featuring an extended operation context.
- **Plug & Play**: Minimalistic configuration setup, extensive customization options
- **High Performance**: Up to 90,000 RPS on Ryzen 5 4650U Pro
- **Memory Optimized**: Minimal footprint, leak-free architecture
- **OpenAPI Compatible**: Partial compatibility with OpenAPI specifications and partial automatic documentation.
- **Working examples**: This project includes a classic Client-Server example, a BlazorWasm-Server example and a triple microservice loop example.

## 📦 Installation

```bash
On client: dotnet add package Hubcon.Client
On server: dotnet add package Hubcon.Server
On your shared project: dotnet add package Hubcon.Shared

You can also install both at the same time, for example, to develop multiple
microservices and ensuring a statically typed integration.
```

## 🏗️ Quick Start

### 1. Define Your Contract
Your contract will be an interface which implements IControllerContract, a marking interface.
We will use this contract later to implement a contract handler in a Controller-like style.
This contract MUST be shared with the client for this to work, and Hubcon will do the rest.

```csharp

public interface IUserContract : IControllerContract
{
    // Standard RPC methods
    // These methods use HTTP by default, but can be switched to websockets on a contract level.
    Task<User> GetUserAsync(int id);
    Task<User> CreateUserAsync(object request);
    
    // Real-time subscriptions
    // They work like normal C# events. You can subscribe or unsubscribe from it.
    // Use UserNotifications.Emit(userNotification) on server-side to send a notification to the client.
    // They are automatically injected when a controller is called, so you can notify the client when executing ANY method.
    // The client has to subscribe to this event, otherwise it will be null on the server.
    ISubscription<int>? UserNotifications { get; }
    
    // Data streaming
    // This can be used to stream any type of serializable object to the client.
    // They work similarly to subscriptions, but they can receive parameters. Perfect for pub/sub.
    IAsyncEnumerable<bool> StreamBooleansAsync(int count);

    // Ingest methods
    // Methods can receive an IAsyncEnumerable<T> directly from the client.
    // Works similarly to subscriptions.
    Task IngestSomethingAsync(IAsyncEnumerable<string> myIngestStream);
}
```

### 2. Server Implementation

## Controller/ContractHandler implementation

Here you will implement your contract/interface, as a normal interface.
```csharp
public class UserController: IUserContract
{
    public async Task<bool> GetUserAsync(int id)
    {
        // Your implementation

        await Task.Delay(10); // Some work done
        return true;
    }

    // Methods can be async or not.
    public Task<bool> CreateUserAsync(object request)
    {
        // Your implementation

        UserNotifications?.Emit(1); // Some notification to the client
        return Task.FromResult(true);
    }

    // This can be null if the client isn't subscribed to this, so mark it as nullable.
    public ISubscription<int>? UserNotifications { get; }
    
    public async IAsyncEnumerable<bool> StreamBooleansAsync(int count)
    {
        // Returns a list of booleans one by one.
        // Can be infinite or you can finish it, and it will finish in the client too.
        await foreach (var myNumber in Enumerable.Range(0, count).Select(x => true))
            yield return myNumber;
    }

    public async Task IngestSomethingAsync(IAsyncEnumerable<string> myIngestStream)
    {
        await foreach(var item in myIngestStream)
            Console.WriteLine(item);

        // Finishes when client finishes, or a websocket silence timeout is reached.
    }
}
```

NOTE: Sync methods are also supported and will work normally on server side, but
they WILL block the main thread on the client, specially on Blazor and
any single-threaded application. The framework will show a warning explaining this.
Task or Task<T> usage is strongly recommended, except on methods that return IAsyncEnumerable<T>,
which already handle this. 

## Server-side program.cs

```csharp
// Before 'var app = builder.Build();'

builder.AddHubconServer();
builder.ConfigureHubconServer(serverOptions =>
{
    serverOptions.ConfigureCore(config => 
    {
        config.EnableRequestDetailedErrors();
        // Here you can configure core features, like:
        // Disabling specific operations, timeouts, message sizes, etc
    });

    // Here you add your Controller/ContractHandler
    serverOptions.AddController<UserController>(configure =>
    {
        // Here you can add custom hubcon middlewares
        // Hubcon middlewares support dependency injection
        // and include an extended operation context.
    });
});
```

```csharp
// After 'builder.Build();'

// Maps and documents normal HTTP endpoints, can be omitted if you only use websockets.
// They are also mapped to OpenAPI, so you can test on Swagger, Scalar or any OpenAPI-compatible tool.
app.MapHubconControllers();

// This enables the hubcon websocket middleware. Can also be omitted if you only want HTTP support.
app.UseHubconWebsockets();
```

These options can be used in any order.

### 3. Client Usage

## Creating a RemoteServerModule
A RemoteServerModule represents a server as an entity. It is used to describe a remote server, and implements
contracts automatically based on it.

```csharp
internal class MyUserServerModule : RemoteServerModule
{
    public override void Configure(IServerModuleConfiguration server)
    {
        // Base url
        server.WithBaseUrl("localhost:5000");

        // Agrego los contratos que este servidor implementa
        // Estos contratos se resuelven por DI con la configuracion puesta en este lugar

        // Here you add your shared contracts. 
        // Any contracts added here will use this server module's configuration.
        server.Implements<IUserContract>(contractConfig =>
        {
            // Here you can change contract-specific settings.
            // For now, you can only switch a contract from HTTP to websockets.
            contractConfig.UseWebsocketMethods(); 
        });

        // Here, you can use an authentication manager which Hubcon will automatically use.
        // Authentication managers can also inject any contracts.
        server.UseAuthenticationManager<AuthenticationManager>();

        // You can switch to insecure connections. This includes 'https -> http' and 'wss to ws' protocols.
        server.UseInsecureConnection();
    }
}
```

## Authentication manager
The authentication manager allows Hubcon to inject an authorization tokens on HTTP requests and
to authenticate the initial websocket connection.

```csharp
public class AuthenticationManager(IUserContract users) : BaseAuthenticationManager
{
    public override string? AccessToken { get; protected set; } = "";
    public override string? RefreshToken { get; protected set; } = "";
    public override DateTime? AccessTokenExpiresAt { get; protected set; } = DateTime.UtcNow.AddYears(1);

    protected async override Task<IAuthResult> AuthenticateAsync(string username, string password)
    {
        // Your login logic
        // users.MyLoginMethod()

        AccessToken = "someToken";
        RefreshToken = "";
        AccessTokenExpiresAt = DateTime.UtcNow.AddYears(1);

        return AuthResult.Success(token, "", 100000);
    }

    protected override Task ClearSessionAsync()
    {
        AccessToken = "";
        RefreshToken = "";
        AccessTokenExpiresAt = null;

        return Task.CompletedTask;
    }

    protected async override Task<PersistedSession?> LoadPersistedSessionAsync()
    {
        // Your session retrieving logic

        AccessToken = "some token";
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
        // Your token refresh logic

        AccessToken = "some token";
        RefreshToken = "";
        AccessTokenExpiresAt = DateTime.UtcNow.AddYears(1);

        return AuthResult.Success(token, "", 100000);
    }

    protected async override Task SaveSessionAsync()
    {
        // You save session logic
    }
}
```
All methods and subscriptions (including ISubscription<T> properties) allow the usage of the
[Authorize] attribute, including it's variants, and the [AllowAnonymous] attribute, for public access.

## Register your RemoteServerModule
```csharp
// On program.cs, before builder.Build()...
builder.Services.AddHubconClient();
builder.Services.AddRemoteServerModule<MyUserServerModule>();
```
And that's it, hubcon will internally implement the server module, and therefore all the specified contracts.

NOTE: There will be exactly 1 client which includes a pooled HTTP client and a custom websocket client per contract.

## 🔧 Advanced Features

### Custom Middleware
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

### Subscription Configuration
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

### WebSocket Reconnection

The hubcon websocket client allows automatic reconnection without breaking ongoing subscriptions, 
which are based on IObservable<T>. They will just wait for the websocket to reconnect and keep receiving messages.

This includes property subscriptions and streams (they will resend the request to restablish them), but will not recover Ingest Methods.

## 📊 Performance

Hubcon is designed for high-performance scenarios:

- **Theoretical ~90,000 RPS** tested on a Ryzen 5 4650U Pro laptop CPU
- **Sub-millisecond latency** for local calls
- **Memory efficient** with zero-allocation hot paths
- **Leak-free** architecture
- **Scalable** subscription management, specially when returning IAsyncEnumerable<T>, allowing parameterized subscriptions.

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
more custom solution. Not to mention it **always broke** the IObservable<T>'s it generated, making everything **harder to implement**.

So i made my own websocket messaging protocol. 

Implemented a better subscription system which doesn't break when the connection is lost,
it just waits for the reconnection and re-subscribes, and everything works as always.

Implemented an ingest system. Servers can receive one or multiple IAsyncEnumerable<T>'s from the client and consume them in multiple tasks.

Implemented a seamless method method calling system, through HTTP or Websockets, as you wish.

Implemented a lightweight custom middleware pipeline with extended details about the operation, and the parsed request.

Just because... **I hate manual integrations**.

## 📄 License

This project is licensed, for now, under a Personal Use License - see the [LICENSE](LICENSE) file for details.

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## 📞 Support

For questions and support, please open an issue on GitHub.
