using Hubcon;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Core.Websockets.Events;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Hubcon.Shared.Core.Tools;

namespace Hubcon.Client.Core.Transports.Websockets
{
    /// <summary>
    /// Hubcon's websocket transport client implementations.
    /// </summary>
    public sealed class WebSocketTransportClient : TransportClient<WebSocketTransport>, IRealTimeTransport
    {
        private P2CPool<HubconWebSocketClient> _clientPool = null!;
        private readonly ILogger<HubconWebSocketClient> logger;

        /// <summary>
        /// Default transport.
        /// </summary>
        /// <param name="logger"></param>
        public WebSocketTransportClient(ILogger<HubconWebSocketClient> logger)
        {
            this.logger = logger;
        }

        /// <inheritdoc/>
        public override async ValueTask CallAsync(IOperationRequest request, IClientOperationContext context,
            CancellationToken cancellationToken = default)
        {
            await _clientPool.ExecuteAsync(static (x, state) => 
                x.SendAsync(state.request, state.context.RemoteCancellationIsAllowed, state.cancellationToken), (request, context, cancellationToken));
            
            await context.SetResponse(HubconResponse.OkT(true));
        }

        /// <inheritdoc/>
        public override async ValueTask<IAsyncEnumerable<JsonElement>> GetStream(IOperationRequest request,
            IClientOperationContext context, CancellationToken cancellationToken = default)
        {
            IObservable<JsonElement> observable;
            observable = await _clientPool.ExecuteAsync(static (x, state) => 
                x.Stream<JsonElement>(state.request, state.context.RemoteCancellationIsAllowed, state.cancellationToken), (request, context, cancellationToken));

            var observer = AsyncObserver.Create<JsonElement>(context.Converter);
            var disposable = observable.Subscribe(observer);
            var enumerable = observer.GetAsyncEnumerable(() => disposable.Dispose());

            await context.SetResponse(HubconResponse.OkT(enumerable));
            return enumerable;
        }

        /// <inheritdoc/>
        public override async ValueTask Ingest<T>(IOperationRequest request, IClientOperationContext context,
            CancellationToken cancellationToken = default)
        {
            var response = await _clientPool.ExecuteAsync(static (x, state) => 
                x.IngestMultiple<JsonElement>(state.request, state.context.RemoteCancellationIsAllowed, state.context.OperationOptions, state.cancellationToken), (request, context, cancellationToken));
            
            await context.HandleResponse<T>(response!);
        }

        /// <inheritdoc/>
        public override async ValueTask SendAsync<T>(IOperationRequest request, IClientOperationContext context,
            CancellationToken cancellationToken = default)
        {
            
            var response = await _clientPool.ExecuteAsync(static (x, state) => 
                x.InvokeAsync<JsonElement>(
                    state.request, 
                    state.context.RemoteCancellationIsAllowed,
                    state.context.ExpectsHubconResponse, 
                    state.cancellationToken), 
                (request, context, cancellationToken));
            
            await context.HandleResponse<T>(response);
        }

        /// <inheritdoc/>
        protected override void Build(TransportContext context)
        {
            _clientPool = new P2CPool<HubconWebSocketClient>(context.ProxyServiceProvider,
                x =>
                {
                    var client = new HubconWebSocketClient(new Uri(context.WebSocketUrl), context, logger);

                    if (context.AuthenticationManagerFactory != null)
                    {
                        var authenticationManager = context.AuthenticationManagerFactory?.Invoke();
                        if (authenticationManager != null)
                        {
                            authenticationManager.OnSessionIsInactive += async () => await client.Disconnect();
                            authenticationManager.OnTokenRefreshed += async (result) =>
                            {
                                if (context.ClientOptions.LoggingEnabled)
                                    logger.LogInformation("Refreshing token in WebSocketTransport...");

                                var response = await client.TryRefreshToken(authenticationManager.TokenType + " " +
                                                                            authenticationManager.AccessToken);

                                if (context.ClientOptions.LoggingEnabled) 
                                    logger.LogInformation($"Token refresh response: {response.Success} | Message: {response.Message}");
                            };
                            // authenticationManager.OnSessionIsActive += async () => await client.EnsureConnectedAsync();

                            client.AuthenticationManagerProvider = () => authenticationManager;
                        }
                    }

                    return client;
                }, context.ClientOptions.MessageProcessorsCount);
        }

        /// <inheritdoc/>
        public async Task<HubconResponse> Connect(string? url = null)
        {
            if (IsConnected())
                return HubconResponse.Ok();

            try
            {
                if (url == null)
                    await _clientPool.ExecuteAllAsync(x => x.EnsureConnectedAsync());
                else
                    await _clientPool.ExecuteAllAsync(x => x.EnsureConnectedAsync(new Uri(url)));

                return HubconResponse.Ok();
            }
            catch (Exception ex)
            {
                return HubconResponse.InternalError(ex, ex.Message);
            }
        }

        /// <inheritdoc/>
        public async Task<HubconResponse> Reconnect(string? url = null)
        {
            try
            {
                if (IsConnected()) await _clientPool.ExecuteAllAsync(static async x => await x.Disconnect());

                if (string.IsNullOrEmpty(url))
                {
                    await _clientPool.ExecuteAllAsync(static async x => await x.EnsureConnectedAsync());
                }
                else
                {
                    await _clientPool.ExecuteAllAsync(async x => await x.EnsureConnectedAsync(new Uri(url)));
                }

                return HubconResponse.Ok();
            }
            catch (Exception ex)
            {
                return HubconResponse.InternalError(ex, ex.Message);
            }
        }

        /// <inheritdoc/>
        public async Task<HubconResponse> Disconnect()
        {
            try
            {
                if (!IsConnected())
                    return HubconResponse.Ok();

                await _clientPool.ExecuteAllAsync(static async x => await x.Disconnect());
                return HubconResponse.Ok();
            }
            catch (Exception ex)
            {
                return HubconResponse.InternalError(ex, ex.Message);
            }
        }

        /// <inheritdoc/>
        public bool IsConnected()
        {
            return _clientPool.ExecuteOnEntries(x => x.All(y => y.Instance.IsConnected));
        }
    }
}