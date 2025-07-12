using Hubcon.Client.Core.Websockets;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Core.Extensions;
using Hubcon.Shared.Core.Websockets.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

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

        private bool IsBuilt { get; set; }
        private IDictionary<Type, IContractOptions>? ContractOptionsDict { get; set; }

        public async Task<T> SendAsync<T>(IOperationRequest request, MethodInfo methodInfo, CancellationToken cancellationToken = default)
        {
            try
            {
                if (ContractOptionsDict?.TryGetValue(methodInfo.ReflectedType!, out var options) ?? false && options.WebsocketMethodsEnabled)
                {
                    if (authenticationManagerFactory?.Invoke() == null)
                        throw new UnauthorizedAccessException("Websockets require authentication by default. Use 'UseAuthorizationManager()' extension method or disable websocket authentication on your server module configuration.");

                    var result = await client.InvokeAsync<BaseOperationResponse<T>>(request);
                    return default!;
                }
                else
                {
                    var bytes = converter.SerializeObject(request.Arguments).ToString();
                    using var content = new StringContent(bytes, Encoding.UTF8, "application/json");

                    HttpMethod httpMethod = request.Arguments.Any() ? HttpMethod.Post : HttpMethod.Get;
                    var url = _restHttpUrl + methodInfo.GetRoute();

                    var httpRequest = new HttpRequestMessage(httpMethod, url)
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

        public async Task CallAsync(IOperationRequest request, MethodInfo methodInfo, CancellationToken cancellationToken = default)
        {
            try
            {
                if (ContractOptionsDict?.TryGetValue(methodInfo.ReflectedType!, out var options) ?? false && options.WebsocketMethodsEnabled)
                {
                    if (authenticationManagerFactory?.Invoke() == null)
                        throw new UnauthorizedAccessException("Websockets require authentication by default. Use 'UseAuthorizationManager()' extension method or disable websocket authentication on your server module configuration.");

                    await client.SendAsync(request);
                }
                else
                {
                    var bytes = converter.SerializeObject(request.Arguments).ToString();
                    using var content = new StringContent(bytes, Encoding.UTF8, "application/json");

                    HttpMethod httpMethod = request.Arguments.Any() ? HttpMethod.Post : HttpMethod.Get;

                    var url = _restHttpUrl + methodInfo.GetRoute();
                    var httpRequest = new HttpRequestMessage(httpMethod, url)
                    {
                        Content = content
                    };

                    var authManager = authenticationManagerFactory?.Invoke();

                    if (authManager != null && authManager.IsSessionActive)
                        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authManager.AccessToken);

                    var response = await httpClient.SendAsync(httpRequest, cancellationToken);

                    var responseBytes = await response.Content.ReadAsByteArrayAsync();
                    var result = converter.DeserializeByteArray<JsonElement>(responseBytes);

                    var methodResponse = converter.DeserializeJsonElement<BaseJsonResponse>(result)!;

                    return;
                }
            }
            catch (Exception ex)
            {
                return;
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

        public async Task Ingest(IOperationRequest request, Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
        {
            if (authenticationManagerFactory?.Invoke() == null)
                throw new UnauthorizedAccessException("Subscriptions are required to be authenticated. Use 'UseAuthorizationManager()' extension method.");

            await client.IngestMultiple(request);
        }


        public async IAsyncEnumerable<JsonElement> GetSubscription(IOperationRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (authenticationManagerFactory?.Invoke() == null)
                throw new UnauthorizedAccessException("Subscriptions are required to be authenticated. Use 'UseAuthorizationManager()' extension method.");

            IObservable<JsonElement> observable = await client.Subscribe<JsonElement>(request);

            var options = new BoundedChannelOptions(5000);

            var observer = new AsyncObserver<JsonElement>(options);

            using (observable.Subscribe(observer))
            {
                await foreach (var newEvent in observer.GetAsyncEnumerable(cancellationToken))
                {
                    var result = newEvent!;
                    yield return result;
                }
            }
        }

        public void Build(Uri BaseUri, 
            string? HttpEndpoint, 
            string? WebsocketEndpoint, 
            Type? AuthenticationManagerType, 
            IServiceProvider services, 
            IDictionary<Type, IContractOptions> contractOptions,
            bool useSecureConnection = true)
        {
            if (IsBuilt) return;

            ContractOptionsDict ??= contractOptions;

            var baseRestHttpUrl = $"{BaseUri!.AbsoluteUri}/{HttpEndpoint ?? ""}".TrimEnd('/');
            var baseRestWebsocketUrl = $"{BaseUri!.AbsoluteUri}/{WebsocketEndpoint ?? "ws"}".TrimEnd('/');

            _restHttpUrl = useSecureConnection ? $"https://{baseRestHttpUrl}" : $"http://{baseRestHttpUrl}";
            _websocketUrl = useSecureConnection ? $"wss://{baseRestWebsocketUrl}" : $"ws://{baseRestWebsocketUrl}";

            if (AuthenticationManagerType is not null)
            {
                var lazyAuthType = typeof(Lazy<>).MakeGenericType(AuthenticationManagerType);
                authenticationManagerFactory = () => (IAuthenticationManager)((dynamic)services.GetRequiredService(lazyAuthType)).Value;
            }

            client = new HubconWebSocketClient(new Uri(_websocketUrl), converter, services.GetRequiredService<ILogger<HubconWebSocketClient>>());

            IsBuilt = true;
        }
    }
}
