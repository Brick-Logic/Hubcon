# Hubcon Framework

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

You can also install both at the same time, for example, to develop multiple microservices and ensuring a statically typed integration.
```

## 🏗️ Quick Start

### 1. Define Your Contract

```csharp
Your contract will be an interface which implement IControllerContract, a marking interface.
We will use this contract later to implement a contract handler in a Controller-like style.
This contract MUST be shared with the client for this to work, and Hubcon will do the rest.

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
}
```

### 2. Server Implementation
Here you will implement your contract/interface, as if it were a normal interface.
```csharp
public class UserContractHandler: IUserContract
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
}

Pending...
```

### 3. Client Usage

```csharp
Pending...
```

## 🔧 Advanced Features

### Custom Middleware

```csharp
Pending...
```

### Subscription Configuration

```csharp
Pending...
```

### WebSocket Reconnection

```csharp
Pending...
```

## 📊 Performance

Hubcon is designed for high-performance scenarios:

- **90,000+ RPS** on commodity hardware (Ryzen 5 4650U Pro)
- **Sub-millisecond latency** for local calls
- **Memory efficient** with zero-allocation hot paths
- **Leak-free** architecture with automatic resource cleanup
- **Scalable** subscription management

## 🔌 Architecture

### Transport Layer
- **HTTP**: RESTful endpoints with JSON serialization
- **WebSocket**: Real-time bidirectional communication
- **Automatic Fallback**: Seamless transport switching

### Contract System
- **Source Generation**: Automatic proxy creation at compile-time
- **Type Safety**: Full compile-time validation
- **Dependency Injection**: Native DI container integration

### Subscription Model
```csharp
Pending...
```

## 🛠️ Configuration

### Server Configuration

```csharp
Pending...
```

### Client Configuration

```csharp
Pending...
```

## 🤝 Integration

### ASP.NET Core Pipeline
Hubcon integrates seamlessly with the ASP.NET Core pipeline:
- Compatible with existing middleware
- Supports authentication and authorization
- Works with model binding and validation
- Integrates with logging and metrics

### Dependency Injection
```csharp
Pending...
```

## 📝 Requirements

- **.NET 8.0** or higher
- **ASP.NET Core 8.0** or higher (server-side)

## 🏆 Why Hubcon?

- **Developer Experience**: Write once, use everywhere contracts, never integrate again
- **Performance**: Optimized for high-throughput scenarios  
- **Flexibility**: HTTP or WebSocket, your choice
- **Real-time**: Built-in subscription and streaming support
- **Maintainable**: Strong typing and compile-time validation
- **Scalable**: Efficient resource management and connection pooling

## 📄 License

This project is licensed under a Personal Use License - see the [LICENSE](LICENSE) file for details.

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## 📞 Support

For questions and support, please open an issue on GitHub.
