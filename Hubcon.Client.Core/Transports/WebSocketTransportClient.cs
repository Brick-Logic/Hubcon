using Hubcon.Client.Core.Websockets;
using Hubcon.Shared.Core.Websockets.Events;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Hubcon.Client.Core.Transports
{
    public sealed class WebSocketTransportClient : TransportClient<WebSocketTransport>
    {
        HubconWebSocketClient _client = null!;
        private readonly ILogger<HubconWebSocketClient> logger;

        public WebSocketTransportClient(ILogger<HubconWebSocketClient> logger)
        {
            this.logger = logger;
        }

        public override async Task<HubconResponse<bool>> CallAsync(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken = default)
        {
            await _client.SendAsync(request, context.RemoteCancellationIsAllowed, cancellationToken);
            return true;
        }

        public override async IAsyncEnumerable<JsonElement> GetStream(IOperationRequest request, IClientOperationContext context, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            IObservable<JsonElement> observable;
            observable = await _client.Stream<JsonElement>(request, context.RemoteCancellationIsAllowed, cancellationToken);

            var observer = AsyncObserver.Create<JsonElement>(context.Converter);
            var enumerable = observer.GetAsyncEnumerable(cancellationToken);

            using (observable.Subscribe(observer))
            {
                var enumerator = enumerable.GetAsyncEnumerator(cancellationToken);
                JsonElement result = default;

                while (true)
                {
                    if (!await enumerator.MoveNextAsync() || cancellationToken.IsCancellationRequested)
                        break;

                    result = enumerator.Current;

                    yield return result;
                }
            }
        }

        public override async IAsyncEnumerable<JsonElement> GetSubscription(IOperationRequest request, IClientOperationContext context, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            IObservable<JsonElement> observable = await _client.Subscribe<JsonElement>(request, context.RemoteCancellationIsAllowed);

            var options = new BoundedChannelOptions(5000);

            var observer = AsyncObserver.Create<JsonElement>(context.Converter, options);

            try
            {
                using (observable.Subscribe(observer))
                {
                    var enumerator = observer.GetAsyncEnumerable(cancellationToken).GetAsyncEnumerator();
                    JsonElement result = default;

                    while (true)
                    {
                        if (!await enumerator.MoveNextAsync())
                            break;

                        result = enumerator.Current;
                        yield return result;
                    }
                }
            }
            finally
            {
                observer.OnCompleted();
            }
        }

        public override async Task<HubconResponse<T>> Ingest<T>(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken = default)
        {
            var response = await _client.IngestMultiple<T>(request, context.RemoteCancellationIsAllowed, context.ClientOptions, context.OperationOptions, cancellationToken);
            return response;
        }

        public override async Task<HubconResponse<T>> SendAsync<T>(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken = default)
        {
            return await _client.InvokeAsync<T>(request, context.RemoteCancellationIsAllowed, cancellationToken);
        }

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

                        var response = await _client.TryRefreshToken(authenticationManager.AccessToken!);

                        if (context.ClientOptions.LoggingEnabled)
                            logger.LogInformation($"Token refresh response: {response.Success} | Message: {response.Message}");
                    };
                    authenticationManager.OnSessionIsActive += async () => await _client.EnsureConnectedAsync();

                    _client.AuthorizationTokenProvider = () => authenticationManager.AccessToken;
                }
            }
        }
    }
}
