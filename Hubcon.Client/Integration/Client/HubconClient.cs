using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Client.Builder;
using Hubcon.Client.Core.Exceptions;
using Hubcon.Client.Core.Websockets;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Core.Extensions;
using Hubcon.Shared.Core.Websockets.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
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
        HubconWebSocketClient client = null!;

        HttpClient? _httpClient;
        HttpClient HttpClient
        {
            get
            {
                if (_httpClient != null)
                    return _httpClient;

                _httpClient ??= clientFactory.CreateClient();
                clientOptions?.HttpClientOptions?.Invoke(_httpClient);

                return _httpClient;
            }
        }

        private IClientOptions? clientOptions { get; set; }

        private bool IsBuilt { get; set; }

        private IDictionary<Type, IContractOptions>? ContractOptionsDict { get; set; }

        public async Task<T> SendAsync<T>(IOperationRequest request, MethodInfo methodInfo, CancellationToken cancellationToken = default)
        {
            if (!IsBuilt)
                throw new InvalidOperationException("El cliente no ha sido construido. Asegúrese de llamar a 'Build()' antes de usar este método.");

            try
            {
                if (ContractOptionsDict?.TryGetValue(methodInfo.ReflectedType!, out var options) ?? false && options.WebsocketMethodsEnabled)
                {
                    if (authenticationManagerFactory?.Invoke() == null)
                        throw new UnauthorizedAccessException("Websockets require authentication by default. Use 'UseAuthorizationManager()' extension method or disable websocket authentication on your server module configuration.");

                    var result = await client.InvokeAsync<T>(request)
                        ?? throw new HubconGenericException("No se recibió ningun mensaje del servidor.");

                    if (!result.Success)
                        throw new HubconRemoteException($"Ocurrió un error en el servidor. Mensaje recibido: {result.Error}");

                    return result.Data!;
                }
                else
                {
                    var bytes = converter.SerializeObject(request.Arguments).ToString();
                    using var content = new StringContent(bytes, Encoding.UTF8, "application/json");

                    HttpMethod httpMethod = request.Arguments!.Any() ? HttpMethod.Post : HttpMethod.Get;
                    var url = _restHttpUrl + methodInfo.GetRoute();

                    var httpRequest = new HttpRequestMessage(httpMethod, url)
                    {
                        Content = content
                    };

                    var authManager = authenticationManagerFactory?.Invoke();

                    if (authManager != null && authManager.IsSessionActive)
                        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authManager.AccessToken);

                    var response = await HttpClient.SendAsync(httpRequest, cancellationToken);

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
                if (ex is HubconRemoteException)
                    throw;
                else if (ex is HubconGenericException)
                    throw;
                else
                    throw new HubconGenericException(ex.Message, ex);
            }

        }

        public async Task CallAsync(IOperationRequest request, MethodInfo methodInfo, CancellationToken cancellationToken = default)
        {
            if (!IsBuilt)
                throw new InvalidOperationException("El cliente no ha sido construido. Asegúrese de llamar a 'Build()' antes de usar este método.");

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
                        httpRequest.Headers.Authorization = new AuthenticationHeaderValue(authManager.TokenType, authManager.AccessToken);

                    await HttpClient.SendAsync(httpRequest, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                if (ex is HubconRemoteException)
                    throw;
                else if (ex is HubconGenericException)
                    throw;
                else
                    throw new HubconGenericException(ex.Message, ex);
            }


        }

        public async IAsyncEnumerable<JsonElement> GetStream(IOperationRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (!IsBuilt)
                throw new InvalidOperationException("El cliente no ha sido construido. Asegúrese de llamar a 'Build()' antes de usar este método.");

            IObservable<JsonElement> observable;

            try
            {
                observable = await client.Stream<JsonElement>(request);
            }
            catch (Exception ex)
            {
                if (ex is HubconRemoteException)
                    throw;
                else if (ex is HubconGenericException)
                    throw;
                else
                    throw new HubconGenericException(ex.Message, ex);
            }

            var observer = AsyncObserver.Create<JsonElement>(converter);

            using (observable.Subscribe(observer))
            {
                var enumerator = observer.GetAsyncEnumerable(cancellationToken).GetAsyncEnumerator();

                while(true)
                {
                    JsonElement result = default;

                    try
                    {
                        if (!await enumerator.MoveNextAsync())
                            break;

                        result = enumerator.Current;
                    }
                    catch (Exception ex)
                    {
                        if (ex is HubconRemoteException)
                            throw;
                        else if (ex is HubconGenericException)
                            throw;
                        else
                            throw new HubconGenericException(ex.Message, ex);
                    }

                    yield return result;
                }
            }
        }

        public async Task<T> Ingest<T>(IOperationRequest request, CancellationToken cancellationToken = default)
        {
            if (!IsBuilt)
                throw new InvalidOperationException("El cliente no ha sido construido. Asegúrese de llamar a 'Build()' antes de usar este método.");
        
            if (authenticationManagerFactory?.Invoke() == null)
                throw new UnauthorizedAccessException("Subscriptions are required to be authenticated. Use 'UseAuthorizationManager()' extension method.");

            var response = await client.IngestMultiple<T>(request);
            return response.Data;          
        }


        public async IAsyncEnumerable<JsonElement> GetSubscription(IOperationRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (!IsBuilt)
                throw new InvalidOperationException("El cliente no ha sido construido. Asegúrese de llamar a 'Build()' antes de usar este método.");

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

            var observer = AsyncObserver.Create<JsonElement>(converter, options);

            try
            {
                using (observable.Subscribe(observer))
                {
                    var enumerator = observer.GetAsyncEnumerable(cancellationToken).GetAsyncEnumerator();

                    while (true)
                    {
                        JsonElement result = default;

                        try
                        {
                            if (!await enumerator.MoveNextAsync())
                                break;

                            result = enumerator.Current;
                        }
                        catch (Exception ex)
                        {
                            if (ex is HubconRemoteException)
                                throw;
                            else if (ex is HubconGenericException)
                                throw;
                            else
                                throw new HubconGenericException(ex.Message, ex);
                        }

                        yield return result;
                    }
                }
            }
            finally
            {
                observer.OnCompleted();
            }
        }

        public void Build(
            IClientOptions builder,
            IServiceProvider services,
            IDictionary<Type, IContractOptions> contractOptions,
            bool useSecureConnection = true)
        {
            if (IsBuilt) return;

            var baseUri = builder.BaseUri;
            var httpEndpoint = builder.HttpPrefix;
            var websocketEndpoint = builder.WebsocketPrefix;
            var authenticationManagerType = builder.AuthenticationManagerType;

            ContractOptionsDict ??= contractOptions;

            var baseRestHttpUrl = $"{baseUri!.AbsoluteUri}/{httpEndpoint ?? ""}".TrimEnd('/');
            var baseRestWebsocketUrl = $"{baseUri!.AbsoluteUri}/{websocketEndpoint ?? "ws"}".TrimEnd('/');

            _restHttpUrl = useSecureConnection ? $"https://{baseRestHttpUrl}" : $"http://{baseRestHttpUrl}";
            _websocketUrl = useSecureConnection ? $"wss://{baseRestWebsocketUrl}" : $"ws://{baseRestWebsocketUrl}";

            if (authenticationManagerType is not null)
            {
                var lazyAuthType = typeof(Lazy<>).MakeGenericType(authenticationManagerType);
                authenticationManagerFactory = () => (IAuthenticationManager)((dynamic)services.GetRequiredService(lazyAuthType)).Value;
            }

            client = new HubconWebSocketClient(new Uri(_websocketUrl), converter, services.GetRequiredService<ILogger<HubconWebSocketClient>>());

            client.AuthorizationTokenProvider = () => authenticationManagerFactory?.Invoke()?.AccessToken;

            client.WebSocketOptions = builder.WebSocketOptions;

            clientOptions = builder;

            IsBuilt = true;
        }
    }
}
