using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Core.Configuration;
using Hubcon.Server.Core.Websockets.Helpers;
using Hubcon.Server.Core.Websockets.Middleware;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Core.Extensions;
using Hubcon.Shared.Core.Tools;
using Hubcon.Shared.Core.Websockets.Events;
using Hubcon.Shared.Core.Websockets.Heartbeat;
using Hubcon.Shared.Core.Websockets.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hubcon.Server.Core.WebSockets.Middleware;

internal sealed class ClientWebSocketContext : IAsyncDisposable
{
    private readonly AtomicPass _isInitialized = new();
    public bool IsInitialized => _isInitialized.WasAcquired;

    private readonly AtomicPass _isDisposed = new();
    public bool IsDisposed => _isDisposed.WasAcquired;

    public bool ConnectionIsClosed => WebSocket?.State != WebSocketState.Open;
    
    public ConcurrentDictionary<Guid, CancellationTokenSource> Streams { get; private set; } = null!;

    public ConcurrentDictionary<Guid, (BaseObservable, CancellationTokenSource, HeartbeatWatcher, RateLimitAttribute)>
        IngestRouters { get; private set; } = null!;

    public ConcurrentDictionary<Guid, CancellationTokenSource> IngestHandlers
    {
        get;
        private set;
    } = null!;

    public ConcurrentDictionary<Guid, IRetryableMessage> AckChannels { get; private set; } = null!;
    public ConcurrentDictionary<Guid, CancellationTokenSource> Tasks { get; private set; } = null!;
    public WebSocketMessageSender Sender { get; private set; } = null!;
    public WebSocketMessageReceiver Receiver { get; private set; } = null!;
    public string ConnectionId { get; private set; } = "";
    public HeartbeatWatcher? Watcher { get; private set; }
    public WebSocket? WebSocket { get; private set; }


    public IOperationConfigRegistry OperationConfigRegistry { get; }
    public IOperationRegistry OperationRegistry { get; }
    public IInternalServerOptions InternalServerOptions { get; }
    public SettingsManager SettingsManager { get; }
    public TimeSpan TimeoutSeconds { get; }
    public HttpContext HttpContext { get; }
    public IDynamicConverter Converter { get; }

    private readonly CancellationTokenSource _cts = new();
    public CancellationToken Token => _cts.Token;

    public IGlobalRateLimiterManager RateLimiter { get; }

    public ILogger<HubconWebSocketMiddleware> Logger { get; }

    public IConnectionSupervisor Supervisor { get; }


    public ClientWebSocketContext(HttpContext context)
    {
        HttpContext = context;
        OperationRegistry = context.RequestServices.GetRequiredService<IOperationRegistry>();
        OperationConfigRegistry = context.RequestServices.GetRequiredService<IOperationConfigRegistry>();
        InternalServerOptions = context.RequestServices.GetRequiredService<IInternalServerOptions>();
        Converter = context.RequestServices.GetRequiredService<IDynamicConverter>();
        RateLimiter = context.RequestServices.GetRequiredService<IGlobalRateLimiterManager>();
        Logger = context.RequestServices.GetRequiredService<ILogger<HubconWebSocketMiddleware>>();
        Supervisor = context.RequestServices.GetRequiredService<IConnectionSupervisor>();

        TimeoutSeconds = InternalServerOptions.WebSocketTimeout;

        SettingsManager = new SettingsManager(OperationRegistry, OperationConfigRegistry);
    }


    public void Initialize(string connectionId, WebSocket webSocket)
    {
        Throw.If(_isDisposed.WasAcquired, static () => new HubconGenericException("This context has already been disposed."));

        if (!_isInitialized.TryAcquirePass()) return;

        ConnectionId = connectionId;
        Streams = new();
        IngestRouters = new();
        IngestHandlers = new();
        AckChannels = new();
        Tasks = new();

        WebSocket = webSocket;
        Sender = new WebSocketMessageSender(webSocket, Converter);
        Receiver = new WebSocketMessageReceiver(webSocket, InternalServerOptions);
    }

    public void EnableHeartbeatWatcher()
    {
        Throw.IfNot(_isInitialized.WasAcquired,
            static () => new HubconGenericException("This web socket context has not been initialized."));
        Throw.If(_isDisposed.WasAcquired,
            static () => new HubconGenericException("This context has already been disposed."));

        Watcher ??= new HeartbeatWatcher(TimeoutSeconds, () =>
        {
            WebSocket?.Abort();
            return _cts.CancelAsync();
        });
    }

    public async Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription)
    {
        Throw.IfNot(_isInitialized.WasAcquired,
            static () => new HubconGenericException("This web socket context has not been initialized."));
        Throw.If(_isDisposed.WasAcquired,
            static () => new HubconGenericException("This context has already been disposed."));

        await WebSocket!.CloseAsync(closeStatus, statusDescription, _cts.Token);
    }

    public AsyncServiceScope CreateAsyncScope()
    {
        return HttpContext.RequestServices.CreateAsyncScope();
    }

    public void Abort()
    {
        Throw.IfNot(_isInitialized.WasAcquired,
            static () => new HubconGenericException("This web socket context has not been initialized."));
        Throw.If(_isDisposed.WasAcquired,
            static () => new HubconGenericException("This context has already been disposed."));

        _cts.Cancel();
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        
        if (Watcher != null)
        {
            try
            {
                await Watcher.DisposeAsync();
            }
            catch
            {
                // ignored
            }
        }
        
        await Supervisor.UnregisterAsync(ConnectionId);

        if (AckChannels != null)
        {
            try
            {
                foreach (var channel in AckChannels)
                {
                    try
                    {
                        if (AckChannels.TryRemove(channel.Key, out var value))
                        {
                            await value.FailedAckAsync();
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
                // Ignored
            }
        }

        if (IngestRouters != null)
        {
            try
            {
                foreach (var task in IngestRouters)
                {
                    if (!IngestRouters.TryRemove(task.Key, out var value)) continue;
                    value.Item1?.OnCompleted();
                    await value.Item3.DisposeAsync();
                    if (!value.Item2.IsCancellationRequested)
                    {
                        value.Item2?.CancelAsync();
                        value.Item2?.Dispose();
                    }

                    await value.Item4.RateBucket.DisposeAsync();
                }
            }
            catch
            {
                // Ignored
            }
        }

        if (IngestHandlers != null)
        {
            try
            {
                foreach (var task in IngestHandlers)
                {
                    if (IngestHandlers.TryRemove(task.Key, out var value))
                    { 
                        value.Dispose();
                    }
                }
            }
            catch
            {
                // Ignored
            }
        }

        if (Tasks != null)
        {
            try
            {
                foreach (var task in Tasks)
                {
                    Tasks.TryRemove(task.Key, out _);
                }
            }
            catch
            {
                // Ignored
            }
        }

        WebSocket?.Dispose();
        _cts.Dispose();
    }
}