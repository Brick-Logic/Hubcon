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
- **High Performance**: Optimized for high throughput, high concurrency stability and low latency.
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
        Task<string> GetUserNameAsync(int id);
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

`Task` or `Task<T>` usage is strongly recommended.

#### ⚪ Server-side program.cs

Before '`var app = builder.Build();`'

```csharp
    builder.AddHubconServer();
    builder.ConfigureHubconServer(serverOptions =>
    {
        serverOptions.AddController<UserController>();
    });
```

After '`builder.Build();`'

```csharp

    // Maps all hubcon controllers to HTTP endpoints.
    app.MapHubconControllers();

    // This enables the hubcon websocket middleware. Not needed for now.
    // app.UseHubconWebsockets();

```

These options can be used in any order and are fully independent.

### 3. Client Usage

#### ⚪ Creating a RemoteServerModule

A `RemoteServerModule` represents a server. It is used to describe a remote server, and implements
one or more contracts automatically based on it.

```csharp
    internal class MyUserServerModule : RemoteServerModule
    {
        public override void Configure(IServerModuleConfiguration server)
        {
            // Base url. Do not specify the protocol.
            server.WithBaseUrl("localhost:5000");

            // Specify the contracts that this server implements. They will share the same configuration and websocket connection.
            server.Implements<IUserContract>();

            // Switch to insecure connection for testing.
            server.UseInsecureConnection();
        }
    }
```

Note: The `Implements<T>` method will automatically generate a client proxy for the specified contract and proceed to register it in the DI container.
All contracts will point to the same server. If you need different servers, you can create multiple `RemoteServerModule`s.

The only limitation is that you **cannot use the same contract on multiple `RemoteServerModules`**. If you do, hubcon will not allow it.

### ⚪ Register your RemoteServerModule

On your client-side `program.cs`...

```csharp
    var builder = WebApplication.CreateBuilder();

    builder.Services.AddHubconClient();
    builder.Services.AddRemoteServerModule<MyUserServerModule>();

    var app = builder.Build();
    var scope = app.Services.CreateScope();

    var client = scope.ServiceProvider.GetRequiredService<IUserContract>();

    var result = await client.GetUserNameAsync(1);

    Console.WriteLine($"User: {result}"); // Should print "User: HubconUser"
    Console.ReadKey();
```

Congratulations! You made hubcon your first client-server call with hubcon.

Hubcon provides a lot of features and configurations that will be explained in the next sections.

## Supported Operations

### Round-trip Operations (Invoke)
Round-trip operations are the most common way to call methods on the server and get a response back.
Works with both HTTP and WebSocket transports (HTTP by default).

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

The client can inject and use the contract anywhere directly to consume the method:

```csharp
    // Inject the client
    var client = scope.ServiceProvider.GetRequiredService<IUserContract>();

    // Call the method
    var userName = await client.GetUserNameAsync(1);

    Console.WriteLine(userName); // Prints "HubconUser"
```

#### Configuration
You can use the `[WebsocketInvokeSettings]` attribute to configure the RPC invocation behavior. Must
be applied to the websocket method.

- `rateTokensPerPeriod` → Tokens generated per period (invoke rate).
- `rateTokenLimit` → Max tokens accumulated (allows bursts).
- `queueLimit` → Max number of requests waiting in the queue.
- `queueProcessingOrder` → Queue order (OldestFirst | NewestFirst).
- `millisecondsToReplenish` → Token refill period duration (default: 1000 ms).

Note that the settings will not working for HTTP invocations, but their behaviour is affected 
directly by the ASP.NET Core pipeline and the Hubcon Pipeline.

### No Return Operations (Call)
Call operations are one-way calls that do not expect a response from the server.
Works with both HTTP and WebSocket transports (HTTP by default).

```csharp
    public interface IUserContract : IControllerContract
    {
        // Some method that takes multiple parameters and has no return.
        Task SendMessage(int id, string message);
    }
```

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

The client can inject and use the contract anywhere directly to consume the method:

```csharp
    // Inject the client
    var client = scope.ServiceProvider.GetRequiredService<IUserContract>();

    // Call the method. HTTP will wait for the response by design, WebSocket will not.
    await client.SendMessage("My message");
```

#### Configuration
You can use the `[WebsocketCallSettings]` attribute to configure the RPC call behavior. Must
be applied to the websocket method.

- `rateTokensPerPeriod` → Tokens generated per period (RPC rate).
- `rateTokenLimit` → Max tokens accumulated (allows bursts).
- `queueLimit` → Max number of requests waiting in the queue.
- `queueProcessingOrder` → Queue order (OldestFirst | NewestFirst).
- `millisecondsToReplenish` → Token refill period duration (default: 1000 ms).

Note that the settings will not working for HTTP calls, but their behaviour is affected 
directly by the ASP.NET Core pipeline and the Hubcon Pipeline.

### Subscriptions
Subscriptions allow the server to push updates to the client in real-time.
You can use the `ISubscription<T>` interface to define a subscription property in your contract.
Subscriptions only work over WebSockets.

```csharp
    public interface IUserContract : IControllerContract
    {
        public ISubscription<int?>? OnUserCreated { get; }
    }
```

To use, just implement the contract and call the Emit() method from the server-side controller:

```csharp
    public class UserContractHandler(ILogger<UserContractHandler> logger) : IUserContract
    {
        public ISubscription<int?>? OnUserCreated { get; }

        // Example method that 'creates' a user and emits an event.
        public Task CreateUser(CancellationToken cancellationToken)
        {
            // Send an event if the client is subscribed (subcribed = not null)
            OnUserCreated?.Emit(1);

            return Task.CompletedTask;
        }
    }
```

Hubcon will detect the presence of the `ISubscription<T>` property and will proceed to implement it automatically.

On the client side, you can subscribe to the event like this:
```csharp
    // We create a handler
    var handler = async (int x) => Console.WriteLine(x);

    // We register the handler for the stream (can be multiple)
    client.OnUserCreated!.AddHandler(handler);

    // Then we subscribe
    await client.OnUserCreated!.Subscribe();

    // Additionaly, you can unsubscribe from the stream
    await client.OnUserCreated!.Unsubscribe();

    // And/or remove the handler
    client.OnUserCreated!.RemoveHandler(handler);
```

Note that users must manually subscribe to start receiving events. If the client isn't subscribed, the `ISubscription<T>` property will be `null`.
If for some reason the connection is lost, the client will automatically re-subscribe when the connection is restored, as long
as the client still chooses to be subscribed. Take in count that re-subscription is based on request resend.

#### Configuration
You can use the `[SubscriptionSettings]` attribute to configure the subscription behavior. Must
be applied to the websocket property.

- `rateTokensPerPeriod` → Tokens generated per period (subscription rate).
- `rateTokenLimit` → Max tokens accumulated (allows bursts).
- `queueLimit` → Max number of requests waiting in the queue.
- `queueProcessingOrder` → Queue order (OldestFirst | NewestFirst).
- `channelCapacity` → Capacity of the internal channel for buffering messages.
- `channelFullMode` → Behavior when the channel is full (Wait | DropOldest | DropNewest | etc.).
- `millisecondsToReplenish` → Token refill period duration (default: 1000 ms).


### Streaming methods
Streaming methods allow the server to push a continuous stream of data to the client.
They only work over WebSockets. 
The only requirement is that the method must return an `IAsyncEnumerable<T>`. Hubcon will do the rest.

#### Usage

```csharp
    public interface IUserContract : IControllerContract
    {
        public IAsyncEnumerable<string> GetMessages(int count);
    }
```

```csharp
    public class UserContractHandler(ILogger<UserContractHandler> logger) : IUserContract
    {
        public async IAsyncEnumerable<string> GetMessages(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return "hello";
            }
        }
    }
```

Because servers are too powerful compared to clients, they can be rate limited to allow slow event sending, or unlocked 
for maximum speed using the `[StreamingSettings]` attribute, acting as a parameterized subscription, or a high-speed stream for arbitrary data.

Streaming methods work similarly to subscriptions, but they can receive parameters and be consumed as an async enumerable.

The client can use `await foreach` to consume the stream:

```csharp
    var messages = client.GetMessages(10);
    await foreach (var message in messages)
    {
        Console.WriteLine(message); // Should print "hello" 10 times
    }
```

Note that if the client disconnects, the server will automatically cancel the stream. 
Streaming methods are not automatically re-subscribed on reconnection.
The usage of `CancellationToken` for resource cleaning is strongly recommended in this case.

#### Configuration
You can use the `[StreamingSettings]` attribute to configure the streaming behavior. Must
be applied to the controller method.

- `rateTokensPerPeriod` → Tokens generated per period (send rate).
- `rateTokenLimit` → Max tokens accumulated (allows bursts).
- `queueProcessingOrder` → Queue order (OldestFirst | NewestFirst).
- `millisecondsToReplenish` → Token refill period duration (default: 1000 ms).

### Ingest Methods

Ingest methods allow the client to send one or more streams of data to the server.
Just ask for an `IAsyncEnumerable<T>` in your contract, and Hubcon will handle the rest.

Ingest methods can be used to send large amounts of data to the server, such as logs, telemetry, or any other data that needs to be processed in real-time.

```csharp
    public interface IUserContract : IControllerContract
    {
        /// No return
        Task IngestMessages(IAsyncEnumerable<string> source, CancellationToken cancellationToken = default);

        // Allows return, just like a normal method.
        Task<bool> IngestMessages(IAsyncEnumerable<string> source, CancellationToken cancellationToken = default);
    }
```

```csharp
    public class UserContractHandler(ILogger<UserContractHandler> logger) : IUserContract
    {
        public async Task IngestMessages(IAsyncEnumerable<string> source, CancellationToken cancellationToken)
        {
            Stopwatch? swReq;

            await foreach (var item in source.WithCancellation(cancellationToken))
            {
                swReq = Stopwatch.StartNew();

                try
                {
                    Interlocked.Increment(ref finishedRequestsCount);
                }
                finally
                {
                    swReq.Stop();
                    latencies.Add(swReq.Elapsed.TotalMilliseconds);
                }
            }

            logger.LogInformation("Ingest finished");
        }
    }
```

On the client side, you can use it like this:
```csharp
    // We create a source of messages
    static async IAsyncEnumerable<string> GetMessages(int count)
    {
        for (int i = 0; i < count; i++)
        {
            yield return "SomeMessage";
            await Task.Delay(100);
        }
    }

    // We call the ingest method
    await client.IngestMessages(GetMessages(1000));
```

From this point, hubcon will start consuming and sending the messages to the server.
The server will receive and process them as they arrive, allowing for real-time data ingestion.

The client-side cancellation token is optional, but recommended for cancelling unlimited ingest operations.

Some notes:
Ingest methods can be throttled to prevent overload, and they support cancellation tokens for resource cleaning.

Ingest methods are not automatically re-subscribed on reconnection and they will be cancelled if the connection is lost. In that case, ingest 
must be restarted from the client.

Clients can also throttle themselves to adjust to the server's rate to prevent flooding, which is important
as the anti-flooding and anti-abuse measures are very aggresive by design on a per-client basis. 

This will be discussed in the later sections.

#### Configuration
You can use the `[IngestSettings]` attribute to configure the ingest behavior. Must
be applied to the controller method.

- `rateTokensPerPeriod` → Tokens generated per period (ingest rate).
- `rateTokenLimit` → Max tokens accumulated (allows bursts).
- `sharedRateLimiter` → Whether to share the rate limiter across methods.
- `queueProcessingOrder` → Queue order (OldestFirst | NewestFirst).
- `channelCapacity` → Capacity of the internal channel for buffering messages.
- `channelFullMode` → Behavior when the channel is full (Wait | DropOldest | DropNewest | etc.).
- `millisecondsToReplenish` → Token refill period duration (default: 1000 ms).


## Authentication and Authorization

### 🔐 Authentication manager
The `AuthenticationManager` allows Hubcon to inject an authorization token on HTTP requests and
to authenticate the initial websocket connection.

```csharp
    public class AuthenticationManager(ISomeAuthContract someAuthContract) : BaseAuthenticationManager
    {
        public override string? TokenType { get; protected set; } = "";
        public override string? AccessToken { get; protected set; } = "";
        public override string? RefreshToken { get; protected set; } = "";
        public override DateTime? AccessTokenExpiresAt { get; protected set; } = DateTime.UtcNow.AddYears(1);

        protected async override Task<IAuthResult> AuthenticateAsync(string username, string password)
        {
            var token = await someAuthContract.LoginAsync(username, password);

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
            var token = await someAuthContract.LoginAsync("aaa", "bbb");

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
            var token = await someAuthContract.LoginAsync(Username, Password);

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
`[Authorize]` attribute, including it's variants, and the `[AllowAnonymous]` attribute. 
`ISubscription<T>` can also use the `[Broadcast]` attribute to allow the subscription to be broadcasted to all clients.

## 🔧 Advanced Features

### ⚪ Custom Middlewares
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

### ⚪ Server Settings
Hubcon allows extensive configuration options to change the framework behavior.

```csharp
    builder.ConfigureHubconServer(serverOptions =>
    {
        // 1️⃣ Register global middlewares
        serverOptions.AddGlobalMiddleware<ExceptionHandlingMiddleware>();
        serverOptions.AddGlobalMiddleware<RequestLoggingMiddleware>();

        // 2️⃣ Register controllers
        serverOptions.AddController<ChatController>(options =>
        {
            options.AddMiddleware<ControllerLoggingMiddleware>();
            options.UseGlobalMiddlewaresFirst(true);
        });

        serverOptions.AddController<OrdersController>();

        // 3️⃣ Configure core server options
        serverOptions.ConfigureCore(config =>
        {
            // 3a. Connection limits
            config.SetMaxWebSocketMessageSize(32_768)
                  .SetMaxHttpMessageSize(64_000);

            // 3b. Timeouts
            config.SetWebSocketTimeout(TimeSpan.FromSeconds(60))
                  .SetHttpTimeout(TimeSpan.FromSeconds(30))
                  .SetWebSocketIngestTimeout(TimeSpan.FromSeconds(45));

            // 3c. Feature toggles
            config.DisableWebSocketIngest(false)
                  .DisableWebSocketSubscriptions(false)
                  .DisableWebSocketMethods(false)
                  .DisableWebSocketStream(false)
                  .DisableWebsocketPing()
                  .DisableWebSocketPong();

            // 3d. Logging & errors
            config.EnableWebsocketsLogging()
                  .EnableHttpLogging()
                  .EnableRequestDetailedErrors(true);

            // 3e. Rate limiting
            config.LimitWebsocketIngest(() => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 500,
                TokensPerPeriod = 500,
                ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                AutoReplenishment = true,
                QueueLimit = 100,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            });

            config.LimitWebsocketRoundTrip(() => new TokenBucketRateLimiterOptions { TokenLimit = 1000, TokensPerPeriod = 1000 });
            config.LimitWebsocketSubscription(() => new TokenBucketRateLimiterOptions { TokenLimit = 200, TokensPerPeriod = 200 });
            config.LimitWebsocketStreaming(() => new TokenBucketRateLimiterOptions { TokenLimit = 1000, TokensPerPeriod = 1000 });

            config.ConfigureWebsocketRateLimiter(() => new TokenBucketRateLimiterOptions { TokenLimit = 500, TokensPerPeriod = 500 });
            config.ConfigureWebsocketPingRateLimiter(() => new TokenBucketRateLimiterOptions { TokenLimit = 50, TokensPerPeriod = 50 });

            // 3f. Security
            config.UseWebsocketTokenHandler((token, sp) =>
            {
                return ValidateToken(token) 
                    ? new ClaimsPrincipal(new ClaimsIdentity("CustomAuth")) 
                    : null;
            });
            config.AllowRemoteTokenCancellation();

            // 3g. Routing
            config.SetWebSocketPathPrefix("/ws")
                  .SetHttpPathPrefix("/api")
                  .UseGlobalRouteHandlerBuilder(builder => { /* custom route builder */ })
                  .UseGlobalHttpConfigurations(ep => { /* global HTTP config */ });
        });
    });
```

#### Cheat Sheet

| Method | Description | Default |
|--------|-------------|---------|
| `SetMaxWebSocketMessageSize(int bytes)` | Maximum WS message size | 16 KB |
| `SetMaxHttpMessageSize(int bytes)` | Maximum HTTP request size | 16 KB |
| `SetWebSocketTimeout(TimeSpan timeout)` | Closes idle WS connections after timeout | 30 s |
| `SetHttpTimeout(TimeSpan timeout)` | HTTP request timeout | 15 s |
| `SetWebSocketIngestTimeout(TimeSpan timeout)` | WS ingest timeout | 30 s |
| `DisableWebSocketIngest(bool disabled = true)` | Enable/disable WS ingest | true |
| `DisableWebSocketSubscriptions(bool disabled = true)` | Enable/disable WS subscriptions | true |
| `DisableWebSocketMethods(bool disabled = true)` | Enable/disable WS RPC methods | true |
| `DisableWebSocketStream(bool disabled = true)` | Enable/disable WS streaming | true |
| `DisableWebsocketPing(bool disabled = true)` | Enable/disable WS ping | false |
| `DisableWebSocketPong(bool disabled = true)` | Enable/disable WS pong | false |
| `EnableWebsocketsLogging(bool enabled = true)` | WS logging | false |
| `EnableHttpLogging(bool enabled = true)` | HTTP logging | false |
| `EnableRequestDetailedErrors(bool enabled = true)` | Include detailed error info | true |
| `LimitWebsocketIngest(Func<TokenBucketRateLimiterOptions> factory)` | Rate limiter for WS ingest | none |
| `LimitWebsocketRoundTrip(Func<TokenBucketRateLimiterOptions> factory)` | Rate limiter for WS RPC | none |
| `LimitWebsocketSubscription(Func<TokenBucketRateLimiterOptions> factory)` | Rate limiter for subscriptions | none |
| `LimitWebsocketStreaming(Func<TokenBucketRateLimiterOptions> factory)` | Rate limiter for streaming | none |
| `ConfigureWebsocketRateLimiter(Func<TokenBucketRateLimiterOptions> factory)` | Global WS limiter | none |
| `ConfigureWebsocketPingRateLimiter(Func<TokenBucketRateLimiterOptions> factory)` | Ping rate limiter | none |
| `DisableAllRateLimiters()` | Remove all WS/HTTP rate limiters | N/A |
| `UseWebsocketTokenHandler(Func<string, IServiceProvider, ClaimsPrincipal?> handler)` | Custom WS authentication | none |
| `AllowRemoteTokenCancellation()` | Allow clients to cancel tokens remotely | false |
| `SetWebSocketPathPrefix(string prefix)` | WS route prefix | "/" |
| `SetHttpPathPrefix(string prefix)` | HTTP route prefix | "/" |
| `UseGlobalRouteHandlerBuilder(Action<RouteHandlerBuilder> configure)` | Customize global WS route builder | none |
| `UseGlobalHttpConfigurations(Action<IEndpointConventionBuilder> configure)` | Global HTTP config | none |


### Client's RemoteServerModule Configuration
The RemoteServerModule can be configured to change the client-side behavior of the connection on a per-contract or per-operation basis.


```csharp
    // Program.cs or Module Setup
    builder.ConfigureRemoteServerModule(module =>
    {
        module
            // Register a contract with optional configuration
            .Implements<IMyContract>(contract =>
            {
                contract
                    // Set the methods transport to websocket as default for this contract
                    .UseWebsocketMethods(true)

                    // Configure operations from this contract
                    .ConfigureOperations(op =>
                    {
                        // Points to a specific method
                        op.Configure(x => x.MyOperation())

                            // Add a custom hook to the operation with DI support
                            .AddHook(HookType.OnSend, async ctx =>
                            {
                                // Custom hook logic before sending
                                Console.WriteLine("Before sending request...");
                            })

                            // Validation hook with DI support to ensure payload is not null
                            .AddValidationHook(async ctx =>
                            {
                                // Validate request
                                if (ctx.Request.Arguments.Values.First() != null)
                                    throw new Exception("Argument can't be null");
                            })

                            // Limit calls per second
                            .LimitPerSecond(100)

                            // Override transport for this operation
                            .UseTransport(TransportType.Websockets) 
                            
                            // Allow remote cancellation for this operation, overrides
                            .AllowRemoteCancellation(); 
                    })

                    // Add hooks to contract level
                    .AddHook(HookType.AfterReceive, async ctx =>
                    {
                        Console.WriteLine("After receiving response");
                    })
                    .AllowRemoteCancellation();
            })

            // Set the server URL
            .WithBaseUrl("https://api.myserver.com")

            // Set HTTP/WebSocket prefixes or endpoints
            .WithHttpPrefix("/api/v1")
            .WithWebsocketEndpoint("/ws/v1")

            // Configure clients
            .ConfigureHttpClient((client, sp) =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .ConfigureWebsocketClient((options, sp) =>
            {
                options.KeepAliveInterval = TimeSpan.FromSeconds(10);
            })

            // Authentication manager
            .UseAuthenticationManager<MyAuthManager>()

            // WebSocket settings
            .SetWebsocketPingInterval(TimeSpan.FromSeconds(5))
            .RequirePongResponse(true)
            .EnableWebsocketAutoReconnect(true)

            // Message processor scaling (1 default, 2 recommended for high traffic)
            .ScaleMessageProcessors(2) 

            // Auto-reconnect for streams/subscriptions/ingest
            .ResubscribeOnReconnect()
            .ResubcribeStreamingOnReconnect()

            // Rate limits (applied per-client)
            .GlobalLimit(500)                          // Global limit per second
            .LimitIngest(200)                           // Messages sent to server
            .LimitSubscription(300)                     // Client subscriptions
            .LimitStreaming(100)                        // Data streaming
            .LimitWebsocketRoundTrip(150)              // WS request-response
            .LimitWebsocketFireAndForget(200)          // WS fire-and-forget

            // Optionally disable all rate limiters
            //.DisableAllLimiters();
    });
```

## Server Settings Cheat Sheet

### Server Configuration
| Method | Description | Default |
|--------|-------------|---------|
| `Implements<T>(Action<IContractConfigurator<T>>?)` | Register a contract interface | none |
| `UseAuthenticationManager<T>()` | Set auth manager for this module | none |
| `WithBaseUrl(string url)` | Base server URL | none |
| `UseInsecureConnection()` | Use HTTP/WS instead of HTTPS/WSS | false |
| `WithHttpPrefix(string prefix)` | HTTP route prefix | `""` |
| `WithWebsocketEndpoint(string endpoint)` | WebSocket endpoint | `"/ws"` |
| `ConfigureHttpClient(Action<HttpClient, IServiceProvider>)` | Customize HTTP client | timeout: 15s |
| `ConfigureWebsocketClient(Action<ClientWebSocketOptions, IServiceProvider>)` | Customize WS client | timeout: 30s, ping interval: 5s |
| `SetWebsocketPingInterval(TimeSpan)` | Interval for WS ping | 5 s |
| `RequirePongResponse(bool)` | Require pong for WS ping | true |
| `EnableWebsocketAutoReconnect(bool)` | Auto reconnect WS | true |
| `ResubscribeOnReconnect(bool)` | Auto reconnect subscriptions | true |
| `ResubcribeStreamingOnReconnect(bool)` | Auto reconnect streams | true |
| `ResubscribeIngestOnReconnect(bool)` | Auto reconnect ingest | true |
| `ScaleMessageProcessors(int)` | Number of message processors | 1 |
| `DisableAllLimiters()` | Disable all rate limiters | false |
| `GlobalLimit(int)` | Global rate limit (msg/sec) | none (default unlimited) |

### Rate Limiters (TokenBucketRateLimiterOptions)
| Method | TokenLimit | TokensPerPeriod | ReplenishmentPeriod | QueueLimit | Notes |
|--------|-----------|----------------|-------------------|------------|-------|
| `WebsocketReaderRateLimiter` | 500 | 500 | 1 s | 1 | WS read operations |
| `WebsocketPingRateLimiter` | 5 | 5 | 5 s | 1 | WS ping messages |
| `WebsocketRoundTripMethodRateLimiter` | 50 | 50 | 1 s | 1 | WS request-response |
| `WebsocketFireAndForgetMethodLimiter` | 100 | 100 | 1 s | 1 | WS fire-and-forget |
| `WebsocketIngestRateLimiter` | 200 | 200 | 1 s | 1 | WS ingest messages |
| `WebsocketSubscriptionRateLimiter` | 20 | 20 | 2 s | 1 | WS subscriptions |
| `WebsocketStreamingRateLimiter` | 100 | 100 | 1 s | 1 | WS streaming |

### Other Defaults
| Property | Default |
|----------|---------|
| `MaxWebSocketMessageSize` | 64 KB |
| `MaxHttpMessageSize` | 128 KB |
| `WebSocketIngestIsAllowed` | true |
| `WebSocketSubscriptionIsAllowed` | true |
| `WebSocketStreamIsAllowed` | true |
| `WebSocketMethodsIsAllowed` | true |
| `WebsocketRequiresPing` | true |
| `WebSocketPongEnabled` | true |
| `MessageRetryIsEnabled` | false |
| `DetailedErrorsEnabled` | false |
| `WebsocketRequiresAuthorization` | false |
| `WebsocketLoggingEnabled` | false |
| `HttpLoggingEnabled` | false |
| `RemoteCancellationIsAllowed` | false |
| `IngestTimeout` | 30 s |


### ⚪ WebSocket Reconnection

The hubcon websocket client allows automatic reconnection without breaking existing subscriptions on the client.
They will just wait for the websocket to reconnect and keep receiving messages.

This includes property subscriptions and streams (they will resend the request to restablish them), but will not recover Ingest Methods.

Note that hubcon's focus is to always keep the client connected. If auto reconnection is disabled, the client will still try
to reconnect when a method that requires websockets is used. 

Methods will always wait for the connection to be established before sending the request.

## 📊 Performance

- **Sub-millisecond latency** for local calls.
- **Memory efficient** with zero-allocation hot paths and minimal memory footprint, leak free optimization.

Hubcon is designed for high-performance scenarios:
                    
- HTTP round-trip: Up to ~66k RPS.
- HTTP one-way call: Up to ~90k RPS.
- Websocket Round-Trip: Up to ~80k RPS.
- Websocket One-Way Call: Up to ~140k RPS.
- Websocket Ingest: ~140k event/s.
- Event Streaming and Subscriptions: Up to ~450k events/s per receiver on client (scalable).

Some notes:
- Tested on a Ryzen 5 5600X CPU.
- Single-threaded client (max 10% CPU).
- 12 threads assigned to server.
- 256 concurrent requests (TPL library). Keeps working even with 65k parallelism level at the cost of latency over websockets.
- HTTP consumes around 50% of the CPU, while WebSockets consume around 33% of the CPU.
- Observed stable ~45mb of RAM in all cases under testing load, both on client and server.

The tests include hooks, remote cancellation coordination, validation hooks, and all features 
configured in the `ClassicExample` project.

Note that `the underlying transport format is JSON`. This is **not ideal** for binary data as the payload is 33% bigger by design, 
but it is more than enough for most use cases. Binary transport is planned for the future, but not yet implemented.

Allocations are kept to a minimum, with most operations being zero-allocation, and the rest being very low allocation.
They will be further optimized to reduce GC pressure and improve performance.

## Self preservation architecture
Hubcon is designed with self-preservation in mind, meaning that it will not allow itself to be overloaded or 
abused by websocket clients.

How does Hubcon protect itself? In websockets, there's a single message processor per client connection. If a client tries
to flood the server with messages, the reader will get stuck by one of the rate limiters, causing the client a natural backpressure due to TCP.

If the server only allows 20 messages per second and the client sends 40, the reader will get stuck waiting for tokens to be available, therefore
not reading any messages in the process, including ping messages, reaching a timeout. 

Also, if there's too many messages in the TCP buffer, the OS will apply backpressure to the client, causing it to slow down.

If the messages keep accumulating, the server will eventually disconnect the client due to timeout or TCP pressure.

However, if the client is well behaved and respects the rate limits, everything will work as expected. 
That's why clients also have rate limiters, to ensure this.

Note that this doesn't apply to HTTP, as HTTP is stateless and each request is independent.

You must use ASP.NET Core's built-in rate limiters to protect your HTTP endpoints. Hubcon middlewares can also help implementing that, but 
using the built-in ones is recommended.

## 🔌 Architecture

### Transport Layer
- **HTTP**: RESTful endpoints with **JSON serialization** with `partial OpenAPI compatibility`
- **WebSocket**: Real-time bidirectional communication using a lightweight messaging protocol.
- **MCP Planned**: A communication protocol that allows AI models and agents to interact with the server.

### Contract System
- **Source Generation**: Automatic minimal proxy generation at compile-time
- **Type Safety**: Full compile-time validation, any incompatible type **will not be tolerated**
- **Dependency Injection**: Seamless DI container integration (needs Autofac)

## 🤝 Integration

### ASP.NET Core Pipeline
Hubcon integrates seamlessly with the ASP.NET Core pipeline:
- Compatible with existing middleware (like Jwt middlewares)
- Supports authentication and authorization
- Integrates with logging and metrics through middlewares.

### Dependency Injection
Just inject the contract you need, and hubcon will magically do the rest.

## 📝 Requirements

- **.NET 8.0** or higher (client and shared project)
- **ASP.NET Core 8.0** or higher (server project)

## 🏆 Why Hubcon?

- **Developer Experience**: Write once what you need, use everywhere, never think about transport again
- **Performance**: Optimized for high-throughput, high-productivity, low ram scenarios.
- **Flexibility**: HTTP or WebSocket, your contract, your choice
- **Real-time**: Built-in subscription and bidirectional streaming support
- **Maintainable**: Strong typing and compile-time validation
- **Scalable**: Efficient resource management and connection pooling
- **Extensible**: Custom middlewares and hooks for advanced scenarios

## Where did Hubcon come from?

Hubcon started as simple controllers for SignalR. I hated having to register every message manually, 
so i made a custom abstract Hub class, which meant controllers for both server and client. 
Both would implement controllers and their contracts.

It was good, I was happy with 1700 RPS at that time. but... 
It was limited in development experience, inefficient, not very flexible and had too many abstractions. 


I left the project for some time, working in jobs, and i got really frustrated with the repetitive integration work.
I thought that this work should already be automated, and i got enough motivation to start this journey.

I first dived into the world of subscriptions and i found GraphQL through HotChocolate, i wanted to use it as a transport layer 
for hubcon by avoiding the model binding and validation for performance, and it worked great, until i saw
how limiting it was for clients and general capabilities. Not to mention how hard it was to simply configure a little, **just a little** 
more custom solution. Not to mention it **always broke** the IObservable<T>'s it generated for subscriptions on the client side, making 
everything **harder to implement and maintain**.

So, i dived into making my own websocket messaging protocol. 

Implemented a better subscription system that doesn't break when the connection is lost, easier to work with, more flexible, 
easier to understand, and most importantly, **way faster**.
If the connection is lost, it just waits for the reconnection and re-subscribes, and everything works as always.

Implemented an ingest system. Servers can receive one or multiple IAsyncEnumerable<T>'s from the client and consume them in multiple tasks.

Implemented a faster method calling system, through HTTP or Websockets, as you wish.

Implemented a lightweight custom middleware pipeline with extended details about the operation, and the parsed request.

Implemented extensive configuration options to change the framework behavior, with huge granularity.

Implemented hooks, rate limiters, authentication, and authorization, and everything with very high performance and 
minimal memory footprint in mind.

Why? Because i hate manual integrations, nothing less, nothing more.

## Project status
This project in a release candidate state, and it will soon be used in real cross-platform projects to show its capabilities.

## 📄 License

This project is licensed, for now, under a Personal Use License - see the [LICENSE](LICENSE) file for details.
This will change in the future when the first stable version is out.

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## 📞 Support

For questions and support, please open an issue on GitHub.