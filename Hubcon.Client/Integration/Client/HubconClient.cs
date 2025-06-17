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
using Hubcon.Shared.Abstractions.Standard.Extensions;

namespace Hubcon.Client.Integration.Client
{
    public class HubconClient(
        IDynamicConverter converter,
        IHttpClientFactory clientFactory,
        ILogger<HubconClient> logger) : IHubconClient
    {
        private string _restHttpUrl = "";
        private string _websocketUrl = "";

        Func<IAuthenticationManager?>? authenticationManagerFactory;
        HubconWebSocketClient client;

        HttpClient httpClient = clientFactory.CreateClient();

        private bool IsStarted { get; set; }
        public async Task<T> SendAsync<T>(IOperationRequest request, MethodInfo methodInfo, CancellationToken cancellationToken = default)
        {
            try
            {
                var bytes = converter.SerializeObject(request.Args).ToString();
                using var content = new StringContent(bytes, Encoding.UTF8, "application/json");

                HttpMethod httpMethod = request.Args.Any() ? HttpMethod.Post : HttpMethod.Get;

                var httpRequest = new HttpRequestMessage(httpMethod, _restHttpUrl + methodInfo.GetRoute())
                {
                    Content = content
                };


                var authManager = authenticationManagerFactory?.Invoke();

                if (authManager != null && authManager.IsSessionActive)
                    httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authManager.AccessToken);

                var response = await httpClient.SendAsync(httpRequest, cancellationToken);

                var responseBytes = await response.Content.ReadAsByteArrayAsync();
                var result = converter.DeserializeByteArray<JsonElement>(responseBytes);

                return converter.DeserializeJsonElement<BaseOperationResponse<T>>(result)!.Data!;
            }
            catch (Exception ex)
            {
                return new BaseOperationResponse<T>(
                    false,
                    default,
                    ex.Message
                ).Data!;
            }

        }

        public async Task<IOperationResponse<JsonElement>> CallAsync(IOperationRequest request, MethodInfo methodInfo, CancellationToken cancellationToken = default)
        {
            try
            {
                var bytes = converter.SerializeObject(request).ToString();
                using var content = new StringContent(bytes, Encoding.UTF8, "application/json");

                HttpMethod httpMethod = request.Args.Any() ? HttpMethod.Post : HttpMethod.Get;

                var httpRequest = new HttpRequestMessage(httpMethod, _restHttpUrl + methodInfo.GetRoute())
                {
                    Content = content
                };

                var authManager = authenticationManagerFactory?.Invoke();

                if (authManager != null && authManager.IsSessionActive)
                    httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authManager.AccessToken);

                var response = await httpClient.SendAsync(httpRequest, cancellationToken);

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
                    var result = newEvent!;
                    yield return result;
                }
            }
        }

        public async Task Ingest(IOperationRequest request, object[] arguments, CancellationToken cancellationToken = default)
        {
            if (authenticationManagerFactory?.Invoke() == null)
                throw new UnauthorizedAccessException("Subscriptions are required to be authenticated. Use 'UseAuthorizationManager()' extension method.");

            await client.IngestMultiple(request, arguments);    
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
                    var result = newEvent!;
                    yield return result;
                }
            }
        }

        private bool IsBuilt { get; set; }
        public void Build(Uri BaseUri, string? HttpEndpoint, string? WebsocketEndpoint, Type? AuthenticationManagerType, IServiceProvider services, bool useSecureConnection = true)
        {
            if (IsBuilt) return;

            var baseRestHttpUrl = $"{BaseUri!.AbsoluteUri}";
            var baseRestWebsocketUrl = $"{BaseUri!.AbsoluteUri}/{WebsocketEndpoint ?? "ws"}";

            _restHttpUrl = useSecureConnection ? $"https://{baseRestHttpUrl}" : $"http://{baseRestHttpUrl}";
            _websocketUrl = useSecureConnection ? $"wss://{baseRestWebsocketUrl}" : $"ws://{baseRestWebsocketUrl}";

            if (AuthenticationManagerType is not null)
            {
                var lazyAuthType = typeof(Lazy<>).MakeGenericType(AuthenticationManagerType);
                authenticationManagerFactory = () => (IAuthenticationManager)((dynamic)services.GetRequiredService(lazyAuthType)).Value;
            }

            client = new HubconWebSocketClient(new Uri(_websocketUrl), converter);

            IsBuilt = true;
        }
    }
}
