using Hubcon.Client.Core.Exceptions;
using Hubcon.Client.Core.Websockets;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Core.Extensions;
using Hubcon.Shared.Core.Websockets.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Reactive.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace Hubcon.Client.Integration.Client
{
    internal sealed class HubconClient(
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

                    var result = await client.InvokeAsync<BaseOperationResponse<T>>(request) 
                        ?? throw new HubconGenericException("No se recibió ningun mensaje del servidor.");
                    
                    if (!result.Success)
                        throw new HubconRemoteException($"Ocurrió un error en el servidor. Mensaje recibido: {result.Error}");

                    return result.Data!;
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

                    if (result.ValueKind == JsonValueKind.Null)
                        throw new HubconGenericException("No se recibió ningun mensaje del servidor.");

                    var operationResponse = converter.DeserializeJsonElement<BaseOperationResponse<T>>(result) 
                        ?? throw new HubconGenericException("No se recibió ningun mensaje del servidor."); ;

                    if (!operationResponse.Success)
                        throw new HubconRemoteException($"Ocurrió un error en el servidor. Mensaje recibido: {operationResponse.Error}");

                    return operationResponse.Data;
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

        public Task CallAsync(IOperationRequest request, MethodInfo methodInfo, CancellationToken cancellationToken = default)
        {
            if (ContractOptionsDict?.TryGetValue(methodInfo.ReflectedType!, out var options) ?? false && options.WebsocketMethodsEnabled)
            {
                if (authenticationManagerFactory?.Invoke() == null)
                    throw new UnauthorizedAccessException("Websockets require authentication by default. Use 'UseAuthorizationManager()' extension method or disable websocket authentication on your server module configuration.");

                return client.SendAsync(request);
            }
            else
            {
                var arguments = converter.Serialize(request.Arguments);
                using var content = new StringContent(arguments, Encoding.UTF8, "application/json");

                HttpMethod httpMethod = request.Arguments!.Any() ? HttpMethod.Post : HttpMethod.Get;

                var url = _restHttpUrl + methodInfo.GetRoute();
                var httpRequest = new HttpRequestMessage(httpMethod, url)
                {
                    Content = content
                };

                var authManager = authenticationManagerFactory?.Invoke();

                if (authManager != null && authManager.IsSessionActive)
                    httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authManager.AccessToken);

                return httpClient.SendAsync(httpRequest, cancellationToken);
            }
        }

        public async IAsyncEnumerable<JsonElement> GetStream(IOperationRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            IObservable<JsonElement> observable; 

            try
            {
                observable = await client.Stream<JsonElement>(request);
            }
            catch (Exception ex)
            {
                throw new HubconGenericException($"Error al obtener el stream del servidor. Mensaje: {ex.Message}", ex);
            }

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

        public Task Ingest(IOperationRequest request, Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
        {
            try
            {
                if (authenticationManagerFactory?.Invoke() == null)
                    throw new UnauthorizedAccessException("Subscriptions are required to be authenticated. Use 'UseAuthorizationManager()' extension method.");

                return client.IngestMultiple(request);
            }
            catch (Exception ex)
            {
                throw new HubconGenericException($"Error al obtener el stream del servidor. Mensaje: {ex.Message}", ex);
            }
        }


        public async IAsyncEnumerable<JsonElement> GetSubscription(IOperationRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            IObservable<JsonElement> observable;

            try
            {
                if (authenticationManagerFactory?.Invoke() == null)
                    throw new UnauthorizedAccessException("Subscriptions are required to be authenticated. Use 'UseAuthorizationManager()' extension method.");

                observable = await client.Subscribe<JsonElement>(request);
            }
            catch (Exception ex)
            {
                throw new HubconGenericException($"Error al obtener el stream del servidor. Mensaje: {ex.Message}", ex);
            }

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

            client.AuthorizationTokenProvider = () => authenticationManagerFactory?.Invoke()?.AccessToken;
            client.WebSocketOptions = x =>
            {
                x.SetBuffer(1024 * 1024, 1024 * 1024);
            };

            IsBuilt = true;
        }
    }
}
