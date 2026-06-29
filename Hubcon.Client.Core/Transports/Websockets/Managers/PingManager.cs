using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Shared.Core.Tools;
using Hubcon.Shared.Core.Websockets.Messages.Ping;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace Hubcon.Client.Core.Transports.Websockets.Managers
{
    /// <summary>
    /// Manages ping/pong messages for the given websocket connection.
    /// </summary>
    public sealed class PingManager : IPingManager
    {
        private readonly IHubconWebSocket webSocket;
        private readonly TransportContext context;
        private readonly ILogger<PingManager>? logger;
        private readonly bool loggingEnabled;
        private System.Timers.Timer? _pingTimer;
        private readonly AtomicPass _disposedPass;
        private readonly AtomicPass _pingStartedPass;
        private readonly CancellationTokenSource _cts;

        public PingManager(IHubconWebSocket webSocket, TransportContext context)
        {
            this.webSocket = webSocket;
            this.context = context;
            logger = context.ProxyServiceProvider.GetService<ILogger<PingManager>>();
            loggingEnabled = context.ClientOptions.LoggingEnabled;
            _cts = new CancellationTokenSource();
            _disposedPass = new AtomicPass();
            _pingStartedPass = new AtomicPass();
        }


        public void Start()
        {
            if (!_pingStartedPass.TryAcquirePass())
                return;

            _pingTimer ??= new System.Timers.Timer();
            _pingTimer.Elapsed += async (_, _) => await PingMessageLoop();
            _pingTimer.Interval = context.ClientOptions.WebsocketPingInterval.TotalMilliseconds;
            _pingTimer.Enabled = true;
            _pingTimer.AutoReset = true;
            _pingTimer.Start();
        }

        private async Task PingMessageLoop()
        {
            if (!_pingStartedPass.WasAcquired || _disposedPass.WasAcquired) return;

            try
            {
                if (webSocket?.State != WebSocketState.Open) return;
                
                await webSocket.SendAsync(new PingMessage(Guid.NewGuid(), webSocket.ConnectionId), false, _cts.Token);

                if (loggingEnabled)
                    logger?.LogInformation("Ping sent.");
                    
                await context.InterceptorManager.CallInterceptor(InterceptorType.OnPing, _cts.Token);
            }
            catch (Exception ex)
            {
                await context.InterceptorManager.CallInterceptor(InterceptorType.OnError, _cts.Token);

                if (loggingEnabled)
                    logger?.LogError(ex.Message);
            }
        }

        /// <summary>
        /// Disposes this object and its resources.
        /// </summary>
        public void Dispose()
        {
            if(!_disposedPass.TryAcquirePass())
                return;

            _cts.Cancel();

            _pingTimer?.Stop();
            _pingTimer?.Dispose();

            _cts.Dispose();

            GC.SuppressFinalize(this);
        }  
    }
}