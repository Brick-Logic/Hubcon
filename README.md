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
TBA
```

## 🏗️ Quick Start

### 1. Define Your Contract

```csharp
public interface IUserContract : IControllerContract
{
    // Standard RPC methods
    Task<User> GetUserAsync(int id);
    Task<User> CreateUserAsync(CreateUserRequest request);
    
    // Real-time subscriptions
    ISubscription<UserNotification> UserNotifications { get; }
    
    // Data streaming
    IAsyncEnumerable<User> StreamUsersAsync(UserFilter filter);
}
```

### 2. Server Implementation

```csharp
public class UserContractHandler: IUserContract
{
    public async Task<User> GetUserAsync(int id)
    {
        // Your implementation
        return await userRepository.GetByIdAsync(id);
    }
    
    public ISubscription<UserNotification> UserNotifications { get; }
    
    public async IAsyncEnumerable<User> StreamUsersAsync(UserFilter filter)
    {
        await foreach (var user in userRepository.StreamAsync(filter))
            yield return user;
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