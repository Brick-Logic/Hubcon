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

namespace Hubcon.Client.Core.Transports.Websockets
{
    /// <summary>
    /// Hubcon's websocket transport client implementations.
    /// </summary>
    public sealed class WebSocketTransportClient : TransportClient<WebSocketTransport>, IRealTimeTransport
    {
        private HubconWebSocketClient _client = null!;
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
        public override async ValueTask CallAsync(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken = default)
        {
            await _client.SendAsync(request, context.RemoteCancellationIsAllowed, cancellationToken);
            await context.SetResponse(HubconResponse.OkT(true));
        }

        /// <inheritdoc/>
        public override async ValueTask<IAsyncEnumerable<JsonElement>> GetStream(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken = default)
        {
            IObservable<JsonElement> observable;
            observable = await _client.Stream<JsonElement>(request, context.RemoteCancellationIsAllowed, cancellationToken);

            var observer = AsyncObserver.Create<JsonElement>(context.Converter);
            var disposable = observable.Subscribe(observer);
            var enumerable = observer.GetAsyncEnumerable(() => disposable.Dispose());

            await context.SetResponse(HubconResponse.OkT(enumerable));
            return enumerable;
        }

        /// <inheritdoc/>
        public override async ValueTask Ingest<T>(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken = default)
        {
            var response = await _client.IngestMultiple<JsonElement>(request, context.RemoteCancellationIsAllowed, context.OperationOptions, cancellationToken);
            await context.HandleResponse<T>(response!);
        }

        /// <inheritdoc/>
        public override async ValueTask SendAsync<T>(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken = default)
        {
            var response = await _client.InvokeAsync<JsonElement>(request, context.RemoteCancellationIsAllowed, context.ExpectsHubconResponse, cancellationToken);
            await context.HandleResponse<T>(response);
        }

        /// <inheritdoc/>
        protected override void Build(TransportContext context)
        {
            _client = new HubconWebSocketClient(new Uri(context.WebSocketUrl), context, logger);

            if (context.AuthenticationManagerFactory != null)
            {
                var authenticationManager = context.AuthenticationManagerFactory?.Invoke();
                if (authenticationManager != null)
                {
                    authenticationManager.OnSessionIsInactive += async () => await _client.Disconnect();
                    authenticationManager.OnTokenRefreshed += async (result) =>
                    {
                        if (context.ClientOptions.LoggingEnabled)
                            logger.LogInformation("Refreshing token in WebSocketTransport...");

                        var response = await _client.TryRefreshToken(authenticationManager.TokenType + " " + authenticationManager.AccessToken);

                        if (context.ClientOptions.LoggingEnabled)
                            logger.LogInformation($"Token refresh response: {response.Success} | Message: {response.Message}");
                    };
                    authenticationManager.OnSessionIsActive += async () => await _client.EnsureConnectedAsync();

                    _client.AuthorizationTokenProvider = () => authenticationManager.TokenType + " " + authenticationManager.AccessToken;
                }
            }
        }

        /// <inheritdoc/>
        public async Task<HubconResponse> Connect(string? url = null)
        {
            if (IsConnected())
                return HubconResponse.Ok();
            
            try
            {
                if (url == null)
                    await _client.EnsureConnectedAsync();
                else
                    await _client.EnsureConnectedAsync(new Uri(url));

                return HubconResponse.Ok();
            }
            catch (Exception ex)
            {
                return HubconResponse.InternalError(ex, ex.Message);
            }
        }

        /// <inheritdoc/>
        public async Task<HubconResponse> Reconnect(string url)
        {
            try
            {
                if(IsConnected()) await _client.Disconnect();
                await _client.EnsureConnectedAsync(new Uri(url));
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
                if(!IsConnected())
                    return HubconResponse.Ok();
                
                await _client.Disconnect();
                return HubconResponse.Ok();
            }
            catch (Exception ex)
            {
                return HubconResponse.InternalError(ex, ex.Message);
            }
        }

        /// <inheritdoc/>
        public bool IsConnected() => _client.IsConnected;     
    }
}
