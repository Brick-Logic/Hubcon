using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Client.Core.Configurations;
using Hubcon.Client.Core.Helpers;
using Hubcon.Client.Core.Websockets;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Core.Websockets.Events;
using Hubcon.Shared.Core.Websockets.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Hubcon.Client.Core.Transports
{
    public sealed class WebSocketTransportClient : ITransportClient<WebSocketTransport>
    {
        HubconWebSocketClient client = null!;
        private readonly IDynamicConverter converter;

        public WebSocketTransportClient(IDynamicConverter converter)
        {
            this.converter = converter;
        }

        private HubconWebSocketClient GetClient(IClientOperationContext context)
        {
            if (client == null)
            {
                ILogger<HubconWebSocketClient> logger = context.ServiceProvider.GetService<ILogger<HubconWebSocketClient>>()!;
                client = new HubconWebSocketClient(new Uri(context.WebSocketUrl), context, logger);

                if (context.AuthenticationManagerFactory != null)
                {
                    var authenticationManager = context.AuthenticationManagerFactory?.Invoke();
                    if (authenticationManager != null)
                    {
                        authenticationManager.OnSessionIsInactive += async () => await client.Disconnect();
                        client.AuthorizationTokenProvider = () => authenticationManager.AccessToken;
                    }
                }
            }

            return client;
        }

        public async Task<HubconResponse<bool>> CallAsync(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken = default)
        {
            var client = GetClient(context);
            context.CallContext.TryRefreshToken ??= client.TryRefreshToken;
            await client.SendAsync(request, context.RemoteCancellationIsAllowed, cancellationToken);
            return true;
        }

        public async IAsyncEnumerable<JsonElement> GetStream(IOperationRequest request, IClientOperationContext context, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var client = GetClient(context);
            IObservable<JsonElement> observable;
            context.CallContext.TryRefreshToken ??= client.TryRefreshToken;

            observable = await client.Stream<JsonElement>(request, context.RemoteCancellationIsAllowed, cancellationToken);

            var observer = AsyncObserver.Create<JsonElement>(converter);
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

        public async IAsyncEnumerable<JsonElement> GetSubscription(IOperationRequest request, IClientOperationContext context, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var client = GetClient(context);
            context.CallContext.TryRefreshToken ??= client.TryRefreshToken;
            IObservable<JsonElement> observable = await client.Subscribe<JsonElement>(request, context.RemoteCancellationIsAllowed);

            var options = new BoundedChannelOptions(5000);

            var observer = AsyncObserver.Create<JsonElement>(converter, options);

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

        public async Task<HubconResponse<T>> Ingest<T>(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken = default)
        {
            var client = GetClient(context);
            context.CallContext.TryRefreshToken ??= client.TryRefreshToken;
            var response = await client.IngestMultiple<T>(request, context.RemoteCancellationIsAllowed, context.ClientOptions, context.OperationOptions, cancellationToken);
            return response;
        }

        public async Task<HubconResponse<T>> SendAsync<T>(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken = default)
        {
            var client = GetClient(context);
            context.CallContext.TryRefreshToken ??= client.TryRefreshToken;
            return await client.InvokeAsync<T>(request, context.RemoteCancellationIsAllowed, cancellationToken);
        }
    }
}
