using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Client.Core.Subscriptions
{
    using System;
    using System.Collections.Concurrent;
    using System.Net.WebSockets;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Reactive.Linq;
    using System.Reactive;
    using Hubcon.Shared.Abstractions.Interfaces;

    public class SubscriptionManager : IDisposable
    {
        private readonly Uri _uri;
        private readonly Func<ClientWebSocket> _webSocketFactory;
        private ClientWebSocket? _webSocket;
        private readonly SemaphoreSlim _connectionLock = new(1, 1);
        private readonly ConcurrentDictionary<string, SubscriptionInfo> _subscriptions = new();
        private CancellationTokenSource? _cts;
        private Task? _receiveLoopTask;
        private Timer? _pingTimer;
        private Timer? _connectionTimeoutTimer;
        private volatile bool _disposed = false;
        private volatile bool _connectionAcknowledged = false;
        private DateTime _lastPongReceived = DateTime.UtcNow;

        // Configurables para GraphQL WebSocket Protocol
        private readonly TimeSpan _pingInterval = TimeSpan.FromSeconds(30);
        private readonly TimeSpan _pongTimeout = TimeSpan.FromSeconds(10);
        private readonly TimeSpan _connectionTimeout = TimeSpan.FromSeconds(15);
        private readonly TimeSpan _reconnectBaseDelay = TimeSpan.FromSeconds(2);
        private readonly TimeSpan _reconnectMaxDelay = TimeSpan.FromSeconds(30);
        private readonly string _subprotocol;

        private readonly IDynamicConverter _converter;
        private readonly Func<IAuthenticationManager?>? _authenticationManagerFactory;

        private record SubscriptionInfo(object Payload, Func<JsonElement, Task> Handler, CancellationTokenSource? CancellationSource = null);

        public ClientWebSocket WebsocketClient => _webSocket ?? throw new InvalidOperationException("WebSocket not initialized");

        public SubscriptionManager(Uri uri, IDynamicConverter converter,
            Func<IAuthenticationManager?>? authenticationManagerFactory = null,
            Func<ClientWebSocket>? factory = null,
            string subprotocol = "graphql-transport-ws") // Default to newer protocol
        {
            _uri = uri ?? throw new ArgumentNullException(nameof(uri));
            _converter = converter ?? throw new ArgumentNullException(nameof(converter));
            _webSocketFactory = factory ?? (() => new ClientWebSocket());
            _authenticationManagerFactory = authenticationManagerFactory;
            _subprotocol = subprotocol;
        }

        public async Task StartAsync()
        {
            ThrowIfDisposed();

            if (_webSocket != null && _webSocket.State == WebSocketState.Open && _connectionAcknowledged)
                return;

            await StopInternalAsync();

            _cts = new CancellationTokenSource();
            await ConnectWithRetryAsync(_cts.Token);
            _receiveLoopTask = Task.Run(() => ReceiveLoopAsync(_cts.Token));
        }

        public async Task StopAsync()
        {
            await StopInternalAsync();
        }

        private async Task StopInternalAsync()
        {
            if (_disposed) return;

            _cts?.Cancel();
            _pingTimer?.Dispose();
            _pingTimer = null;
            _connectionTimeoutTimer?.Dispose();
            _connectionTimeoutTimer = null;

            // Send connection_terminate before closing
            if (_webSocket != null && _webSocket.State == WebSocketState.Open)
            {
                try
                {
                    await SendMessageAsync(new { type = "connection_terminate" });
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing",
                        new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token);
                }
                catch { /* Ignore exceptions on close */ }
            }

            if (_receiveLoopTask != null)
            {
                try
                {
                    await _receiveLoopTask;
                }
                catch { /* Task might be canceled */ }
            }

            _webSocket?.Dispose();
            _webSocket = null;
            _cts?.Dispose();
            _cts = null;
            _connectionAcknowledged = false;

            // Cancel all active subscriptions
            foreach (var subscription in _subscriptions.Values)
            {
                subscription.CancellationSource?.Cancel();
                subscription.CancellationSource?.Dispose();
            }
            _subscriptions.Clear();
        }

        private void StartPingTimer()
        {
            _pingTimer?.Dispose();
            _pingTimer = new Timer(async _ =>
            {
                if (_disposed || _webSocket?.State != WebSocketState.Open) return;

                try
                {
                    // Check if we received a pong recently
                    if (DateTime.UtcNow - _lastPongReceived > _pongTimeout)
                    {
                        // Connection seems dead, force reconnect
                        _cts?.Cancel();
                        return;
                    }

                    await SendMessageAsync(new { type = "ping" });
                }
                catch
                {
                    // Ping failed, let receive loop handle reconnection
                    _cts?.Cancel();
                }
            }, null, _pingInterval, _pingInterval);
        }

        private void StartConnectionTimeoutTimer()
        {
            _connectionTimeoutTimer?.Dispose();
            _connectionTimeoutTimer = new Timer(_ =>
            {
                if (!_connectionAcknowledged && !_disposed)
                {
                    // Connection not acknowledged in time, force reconnect
                    _cts?.Cancel();
                }
            }, null, _connectionTimeout, Timeout.InfiniteTimeSpan);
        }

        private async Task ConnectWithRetryAsync(CancellationToken token)
        {
            var delay = _reconnectBaseDelay;
            var attempt = 1;

            while (!token.IsCancellationRequested && !_disposed)
            {
                try
                {
                    Console.WriteLine($"Attempting connection #{attempt}...");
                    await ConnectAsync(token);
                    Console.WriteLine("Connection established successfully");
                    return;
                }
                catch (Exception ex) when (!token.IsCancellationRequested && !_disposed)
                {
                    Console.WriteLine($"Connection attempt #{attempt} failed: {ex.Message}");
                    try
                    {
                        await Task.Delay(delay, token);
                        delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 1.5, _reconnectMaxDelay.TotalSeconds));
                        attempt++;
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }
            }
        }

        private async Task ConnectAsync(CancellationToken token)
        {
            await _connectionLock.WaitAsync(token);
            CancellationTokenSource? ackTimeout = null;
            CancellationTokenSource? combinedTokenSource = null;

            try
            {
                _webSocket?.Dispose();
                _webSocket = _webSocketFactory();
                _connectionAcknowledged = false;

                // Configure WebSocket for GraphQL
                _webSocket.Options.AddSubProtocol(_subprotocol);
                _webSocket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);

                // Set headers
                var authManager = _authenticationManagerFactory?.Invoke();
                if (authManager != null && authManager.IsSessionActive)
                {
                    _webSocket.Options.SetRequestHeader("Authorization", $"Bearer {authManager.AccessToken}");
                }

                // Additional headers for GraphQL WebSocket
                _webSocket.Options.SetRequestHeader("Sec-WebSocket-Protocol", _subprotocol);

                await _webSocket.ConnectAsync(_uri, token);

                // Start timeout timer for connection_ack
                StartConnectionTimeoutTimer();

                await SendInitMessageAsync(token);

                // Wait for connection_ack before proceeding
                ackTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                combinedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token, ackTimeout.Token);

                try
                {
                    while (!_connectionAcknowledged && !combinedTokenSource.Token.IsCancellationRequested)
                    {
                        await Task.Delay(100, combinedTokenSource.Token);
                    }

                    // Check why we exited the loop
                    if (token.IsCancellationRequested)
                    {
                        throw new OperationCanceledException("Connection was cancelled", token);
                    }

                    if (!_connectionAcknowledged)
                    {
                        throw new TimeoutException("Connection acknowledgment not received within timeout");
                    }
                }
                catch (OperationCanceledException) when (ackTimeout.Token.IsCancellationRequested && !token.IsCancellationRequested)
                {
                    // Only the ack timeout was cancelled, not the main token
                    throw new TimeoutException("Connection acknowledgment timeout");
                }

                await ResubscribeAllAsync(token);
                StartPingTimer();
                _lastPongReceived = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Connection failed: {ex.Message}");

                // Clean up partial connection
                _connectionAcknowledged = false;
                _connectionTimeoutTimer?.Dispose();
                _connectionTimeoutTimer = null;

                if (_webSocket != null)
                {
                    try
                    {
                        if (_webSocket.State == WebSocketState.Open)
                        {
                            await _webSocket.CloseAsync(WebSocketCloseStatus.InternalServerError, "Connection failed",
                                CancellationToken.None);
                        }
                    }
                    catch { /* Ignore close errors */ }

                    _webSocket.Dispose();
                    _webSocket = null;
                }

                throw; // Re-throw the original exception
            }
            finally
            {
                ackTimeout?.Dispose();
                combinedTokenSource?.Dispose();
                _connectionLock.Release();
            }
        }

        private async Task SendInitMessageAsync(CancellationToken token)
        {
            var authManager = _authenticationManagerFactory?.Invoke();
            var payload = new Dictionary<string, object>();

            if (authManager != null && authManager.IsSessionActive)
            {
                payload["Authorization"] = $"Bearer {authManager.AccessToken}";
            }

            var init = new
            {
                type = "connection_init",
                payload = payload.Count > 0 ? payload : new()
            };

            await SendMessageAsync(init, token);
        }

        public IObservable<T> SubscribeAsync<T>(object payload)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(payload);

            var id = Guid.NewGuid().ToString();

            return Observable.Create<T>(async (observer, cancellationToken) =>
            {
                var subscriptionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                try
                {
                    async Task Handler(JsonElement jsonPayload)
                    {
                        if (subscriptionCts.Token.IsCancellationRequested) return;

                        try
                        {
                            var data = _converter.DeserializeData<T>(jsonPayload.GetRawText());
                            if (data != null)
                            {
                                observer.OnNext(data);
                            }
                        }
                        catch (Exception ex)
                        {
                            observer.OnError(ex);
                        }
                    }

                    // Wait for connection to be ready
                    while (!_connectionAcknowledged && !cancellationToken.IsCancellationRequested)
                    {
                        await Task.Delay(100, cancellationToken);
                    }

                    await SubscribeAsync(id, payload, Handler, subscriptionCts);

                    return async () =>
                    {
                        try
                        {
                            subscriptionCts.Cancel();
                            await UnsubscribeAsync(id);
                            observer.OnCompleted();
                        }
                        finally
                        {
                            subscriptionCts.Dispose();
                        }
                    };
                }
                catch
                {
                    subscriptionCts.Dispose();
                    throw;
                }
            });
        }

        public async Task SubscribeAsync(string id, object payload, Func<JsonElement, Task> handler,
            CancellationTokenSource? cancellationSource = null)
        {
            ThrowIfDisposed();
            ArgumentException.ThrowIfNullOrEmpty(id);
            ArgumentNullException.ThrowIfNull(payload);
            ArgumentNullException.ThrowIfNull(handler);

            _subscriptions[id] = new SubscriptionInfo(payload, handler, cancellationSource);

            var message = new
            {
                id,
                type = "start", // Use 'start' for graphql-ws, 'subscribe' for graphql-transport-ws
                payload
            };

            await SendMessageAsync(message);
        }

        public async Task UnsubscribeAsync(string id)
        {
            if (_disposed || string.IsNullOrEmpty(id)) return;

            if (_subscriptions.TryRemove(id, out var subscription))
            {
                subscription.CancellationSource?.Cancel();
                subscription.CancellationSource?.Dispose();
            }

            if (_webSocket?.State == WebSocketState.Open)
            {
                try
                {
                    var message = new
                    {
                        id,
                        type = "stop" // Use 'stop' for graphql-ws, 'complete' for graphql-transport-ws
                    };

                    await SendMessageAsync(message);
                }
                catch
                {
                    // Ignore errors when unsubscribing
                }
            }
        }

        private async Task SendMessageAsync(object message, CancellationToken? token = null)
        {
            ThrowIfDisposed();

            if (_webSocket == null || _webSocket.State != WebSocketState.Open)
                throw new InvalidOperationException("WebSocket is not connected");

            var json = _converter.SerializeData(message) ?? throw new InvalidOperationException("Failed to serialize message");
            var buffer = Encoding.UTF8.GetBytes(json);

            // Send in chunks if message is large
            const int maxChunkSize = 4096;
            var offset = 0;

            while (offset < buffer.Length)
            {
                var chunkSize = Math.Min(maxChunkSize, buffer.Length - offset);
                var isLastChunk = offset + chunkSize >= buffer.Length;
                var segment = new ArraySegment<byte>(buffer, offset, chunkSize);

                await _webSocket.SendAsync(segment, WebSocketMessageType.Text, isLastChunk,
                    token ?? CancellationToken.None);

                offset += chunkSize;
            }
        }

        private async Task ReceiveLoopAsync(CancellationToken token)
        {
            var buffer = new byte[16384]; // Increased buffer size
            var messageBuilder = new StringBuilder();

            while (!token.IsCancellationRequested && !_disposed)
            {
                if (_webSocket == null || _webSocket.State != WebSocketState.Open)
                {
                    await ConnectWithRetryAsync(token);
                    continue;
                }

                try
                {
                    WebSocketReceiveResult result;
                    messageBuilder.Clear();

                    // Handle potentially fragmented messages
                    do
                    {
                        result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), token);

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            Console.WriteLine($"WebSocket closed: {result.CloseStatus} - {result.CloseStatusDescription}");
                            await ConnectWithRetryAsync(token);
                            break;
                        }

                        if (result.MessageType == WebSocketMessageType.Text)
                        {
                            var chunk = Encoding.UTF8.GetString(buffer, 0, result.Count);
                            messageBuilder.Append(chunk);
                        }
                    } while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Close) continue;

                    var message = messageBuilder.ToString();
                    if (!string.IsNullOrEmpty(message))
                    {
                        using var doc = JsonDocument.Parse(message);
                        await ProcessMessage(doc);
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (WebSocketException wsEx)
                {
                    Console.WriteLine($"WebSocket error: {wsEx.Message}");
                    if (!_disposed)
                    {
                        await Task.Delay(2000, token);
                        await ConnectWithRetryAsync(token);
                    }
                }
                catch (Exception ex) when (!_disposed)
                {
                    Console.WriteLine($"Receive error: {ex.Message}");
                    try
                    {
                        await Task.Delay(1000, token);
                        await ConnectWithRetryAsync(token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }

        private async Task ProcessMessage(JsonDocument doc)
        {
            if (!doc.RootElement.TryGetProperty("type", out var typeElement))
                return;

            var type = typeElement.GetString();

            switch (type)
            {
                case "connection_ack":
                    _connectionAcknowledged = true;
                    _connectionTimeoutTimer?.Dispose();
                    Console.WriteLine("GraphQL WebSocket connection acknowledged");
                    break;

                case "connection_error":
                    Console.WriteLine("GraphQL WebSocket connection error");
                    _cts?.Cancel(); // Force reconnection
                    break;

                case "data": // graphql-ws protocol
                case "next": // graphql-transport-ws protocol
                    if (doc.RootElement.TryGetProperty("id", out var idElement))
                    {
                        var id = idElement.GetString();
                        if (id != null && _subscriptions.TryGetValue(id, out var sub))
                        {
                            if (sub.CancellationSource?.Token.IsCancellationRequested == true)
                                return;

                            if (doc.RootElement.TryGetProperty("payload", out var payload))
                            {
                                try
                                {
                                    await sub.Handler(payload);
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Handler error for subscription {id}: {ex.Message}");
                                }
                            }
                        }
                    }
                    break;

                case "error":
                    if (doc.RootElement.TryGetProperty("id", out var errIdElem))
                    {
                        var errId = errIdElem.GetString();
                        Console.WriteLine($"Subscription error for {errId}");
                        // TODO: Propagate error to specific subscription observer
                    }
                    break;

                case "complete":
                    if (doc.RootElement.TryGetProperty("id", out var compIdElem))
                    {
                        var compId = compIdElem.GetString();
                        if (compId != null && _subscriptions.TryGetValue(compId, out var subscription))
                        {
                            subscription.CancellationSource?.Cancel();
                            _subscriptions.TryRemove(compId, out _);
                            Console.WriteLine($"Subscription {compId} completed");
                        }
                    }
                    break;

                case "ping":
                    try
                    {
                        await SendMessageAsync(new { type = "pong" });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to send pong: {ex.Message}");
                    }
                    break;

                case "pong":
                    _lastPongReceived = DateTime.UtcNow;
                    break;

                case "ka": // Keep alive from graphql-ws
                    _lastPongReceived = DateTime.UtcNow;
                    break;
            }
        }

        private async Task ResubscribeAllAsync(CancellationToken token)
        {
            foreach (var (id, subscriptionInfo) in _subscriptions)
            {
                if (subscriptionInfo.CancellationSource?.Token.IsCancellationRequested == true)
                    continue;

                try
                {
                    var message = new
                    {
                        id,
                        type = "start", // Adjust based on protocol
                        payload = subscriptionInfo.Payload
                    };
                    await SendMessageAsync(message, token);
                    Console.WriteLine($"Resubscribed to {id}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to resubscribe {id}: {ex.Message}");
                }
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SubscriptionManager));
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            try
            {
                StopInternalAsync().GetAwaiter().GetResult();
            }
            catch { /* Ignore disposal errors */ }

            _connectionLock.Dispose();

            GC.SuppressFinalize(this);
        }
    }

}