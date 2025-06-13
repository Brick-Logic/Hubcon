using Hubcon.Client.Core.Websockets;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Core.Subscriptions;
using Hubcon.Shared.Entrypoint;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace Hubcon.Client.Integration.Client
{
    public class HubconClient(
        IDynamicConverter converter,
        Lazy<HttpClient> clientFactory,
        ILogger<HubconClient> logger) : IHubconClient
    {
        private string _restHttpUrl = "";
        private string _websocketUrl = "";

        Func<IAuthenticationManager?>? authenticationManagerFactory;
        HubconWebSocketClient client;

        private bool IsStarted { get; set; }
        public async Task<IOperationResponse<JsonElement>> SendAsync(IOperationRequest request, MethodInfo methodInfo, CancellationToken cancellationToken = default)
        {
            try
            {
                var bytes = converter.SerializeObject(request).ToString();
                using var content = new StringContent(bytes, Encoding.UTF8, "application/json");

                var httpRequest = new HttpRequestMessage(HttpMethod.Post, _restHttpUrl + $"/{nameof(DefaultEntrypoint.HandleMethodWithResult)}")
                {
                    Content = content
                };

                var authManager = authenticationManagerFactory?.Invoke();

                if (authManager != null && authManager.IsSessionActive)
                    httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authManager.AccessToken);


                var client = clientFactory.Value;
                var response = await client.SendAsync(httpRequest, cancellationToken);

                var responseBytes = await response.Content.ReadAsByteArrayAsync();
                var result = converter.DeserializeByteArray<JsonElement>(responseBytes);

                return converter.DeserializeJsonElement<BaseJsonResponse>(result)!;
            }
            catch (Exception ex)
            {
                return new BaseJsonResponse(
                    false,
                    default,
                    ex.Message
                );
            }

        }

        public async Task<IOperationResponse<JsonElement>> CallAsync(IOperationRequest request, MethodInfo methodInfo, CancellationToken cancellationToken = default)
        {
            try
            {
                var bytes = converter.SerializeObject(request).ToString();
                using var content = new StringContent(bytes, Encoding.UTF8, "application/json");

                var httpRequest = new HttpRequestMessage(HttpMethod.Post, _restHttpUrl + $"/{nameof(DefaultEntrypoint.HandleMethodVoid)}")
                {
                    Content = content
                };

                var authManager = authenticationManagerFactory?.Invoke();

                if (authManager != null && authManager.IsSessionActive)
                    httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authManager.AccessToken);


                var client = clientFactory.Value;
                var response = await client.SendAsync(httpRequest, cancellationToken);

                var responseBytes = await response.Content.ReadAsByteArrayAsync();
                var result = converter.DeserializeByteArray<JsonElement>(responseBytes);

                return converter.DeserializeJsonElement<BaseJsonResponse>(result)!;
            }
            catch (Exception ex)
            {
                return new BaseJsonResponse(
                    false,
                    default,
                    ex.Message
                );
            }

        }

        public async IAsyncEnumerable<JsonElement> GetStream(IOperationRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            IObservable<JsonElement> observable = await client.Stream<JsonElement>(request);

            var observer = new AsyncObserver<JsonElement>();

            using (observable.Subscribe(observer))
            {
                await foreach (var newEvent in observer.GetAsyncEnumerable(cancellationToken))
                {
                    var result = newEvent!.Clone();
                    yield return result;
                }
            }
        }

        public async IAsyncEnumerable<JsonElement> GetSubscription(IOperationRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (authenticationManagerFactory?.Invoke() == null)
                throw new UnauthorizedAccessException("Subscriptions are required to be authenticated. Use 'UseAuthorizationManager()' extension method.");

            IObservable<JsonElement> observable = await client.Subscribe<JsonElement>(request);

            var observer = new AsyncObserver<JsonElement>();

            using (observable.Subscribe(observer))
            {
                await foreach (var newEvent in observer.GetAsyncEnumerable(cancellationToken))
                {
                    var result = newEvent!.Clone();
                    yield return result;
                }
            }
        }

        private bool IsBuilt { get; set; }
        public void Build(Uri BaseUri, string? HttpEndpoint, string? WebsocketEndpoint, Type? AuthenticationManagerType, IServiceProvider services, bool useSecureConnection = true)
        {
            if (IsBuilt) return;

            var baseRestHttpUrl = $"{BaseUri!.AbsoluteUri}/{HttpEndpoint ?? "operation"}";
            var baseRestWebsocketUrl = $"{BaseUri!.AbsoluteUri}/{WebsocketEndpoint ?? "ws"}";

            _restHttpUrl = useSecureConnection ? $"https://{baseRestHttpUrl}" : $"http://{baseRestHttpUrl}";
            _websocketUrl = useSecureConnection ? $"wss://{baseRestWebsocketUrl}" : $"ws://{baseRestWebsocketUrl}";

            if (AuthenticationManagerType is not null)
            {
                var lazyAuthType = typeof(Lazy<>).MakeGenericType(AuthenticationManagerType);
                authenticationManagerFactory = () => (IAuthenticationManager)((dynamic)services.GetRequiredService(lazyAuthType)).Value;
            }

            client = new HubconWebSocketClient(new Uri(_websocketUrl));

            IsBuilt = true;
        }
    }
}
