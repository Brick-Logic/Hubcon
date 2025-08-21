using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Client.Core.Exceptions;
using Hubcon.Client.Core.Helpers;
using Hubcon.Client.Core.Websockets;
using Hubcon.Shared.Abstractions.Enums;
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
    internal sealed class HubconClient(IDynamicConverter converter, IHttpClientFactory clientFactory, ILogger<HubconClient> logger) : IHubconClient
    {
        private string _restHttpUrl = "";
        private string _websocketUrl = "";

        Func<IAuthenticationManager?>? authenticationManagerFactory;
        
        IAuthenticationManager? _authenticationManager;
        IAuthenticationManager AuthenticationManager => _authenticationManager 
            ??= authenticationManagerFactory?.Invoke() 
            ?? throw new InvalidOperationException($"Authentication Manager not defined for server module '{clientOptions.ServerModuleName}'.");
        
        HubconWebSocketClient client = null!;

        HttpClient? _httpClient;
        HttpClient HttpClient
        {
            get
            {
                if (_httpClient != null)
                    return _httpClient;

                _httpClient ??= clientFactory.CreateClient();
                clientOptions?.HttpClientOptions?.Invoke(_httpClient, ServiceProvider);

                return _httpClient;
            }
        }

        private IServiceProvider ServiceProvider { get; set; } = null!;

        private IClientOptions clientOptions { get; set; } = null!;

        private bool IsBuilt { get; set; }

        private IDictionary<Type, IContractOptions> ContractOptionsDict { get; set; } = null!;

        public async Task<T> SendAsync<T>(IOperationRequest request, MethodInfo methodInfo, CancellationToken cancellationToken)
        {           
            IOperationOptions? operationOptions = null;
            ContractOptionsDict!.TryGetValue(methodInfo.ReflectedType!, out IContractOptions? contractOptions);

            bool isWebsocketMethod = false;
            
            if (contractOptions != null)
            {
                isWebsocketMethod = contractOptions.IsWebsocketOperation(request.OperationName);
                operationOptions = contractOptions.GetOperationOptions(request.OperationName);
            }

            bool remoteCancellation = operationOptions?.RemoteCancellationIsAllowed 
                                      ?? contractOptions?.RemoteCancellationIsAllowed 
                                      ?? false;
            
            await CallValidationHook(operationOptions, ServiceProvider, request, cancellationToken);

            try
            {
                if (isWebsocketMethod)
                {
                    await RateLimiterHelper.AcquireAsync(clientOptions, clientOptions?.RateBucket, clientOptions?.WebsocketRoundTripRateBucket, operationOptions?.RateBucket);
                    
                    await CallHook(operationOptions, HookType.OnSend, ServiceProvider, request, cancellationToken);
                    await CallHook(contractOptions, HookType.OnSend, ServiceProvider, request, cancellationToken);

                    var result = await client.InvokeAsync<T>(request, remoteCancellation, cancellationToken);

                    await CallHook(operationOptions, HookType.OnAfterSend, ServiceProvider, request, cancellationToken);
                    await CallHook(contractOptions, HookType.OnAfterSend, ServiceProvider, request, cancellationToken);

                    if (!result.Success)
                        throw new HubconRemoteException($"Ocurrió un error en el servidor. Mensaje recibido: {result.Error}");

                    await CallHook(operationOptions, HookType.OnResponse, ServiceProvider, request, cancellationToken, result.Data);
                    await CallHook(contractOptions, HookType.OnResponse, ServiceProvider, request, cancellationToken, result.Data);

                    return result.Data!;
                }
                else
                {
                    await RateLimiterHelper.AcquireAsync(clientOptions, clientOptions?.RateBucket, clientOptions?.HttpRoundTripRateBucket, operationOptions?.RateBucket);

                    var bytes = converter.SerializeToElement(request.Arguments).ToString();
                    using var content = new StringContent(bytes, Encoding.UTF8, "application/json");

                    HttpMethod httpMethod = request.Arguments!.Any() ? HttpMethod.Post : HttpMethod.Get;
                    var url = _restHttpUrl + methodInfo.GetRoute();

                    var httpRequest = new HttpRequestMessage(httpMethod, url)
                    {
                        Content = content
                    };
                    
                    if (AuthenticationManager.IsSessionActive)
                        httpRequest.Headers.Authorization = new AuthenticationHeaderValue(AuthenticationManager.TokenType!, AuthenticationManager.AccessToken);

                    await CallHook(operationOptions, HookType.OnSend, ServiceProvider, request, cancellationToken);
                    await CallHook(contractOptions, HookType.OnSend, ServiceProvider, request, cancellationToken);

                    HttpResponseMessage response = await HttpClient.SendAsync(httpRequest, cancellationToken);

                    await CallHook(operationOptions, HookType.OnAfterSend, ServiceProvider, request, cancellationToken);
                    await CallHook(contractOptions, HookType.OnAfterSend, ServiceProvider, request, cancellationToken);

                    var responseBytes = await response.Content.ReadAsByteArrayAsync();
                    var result = converter.DeserializeByteArray<JsonElement>(responseBytes);

                    if (result.ValueKind == JsonValueKind.Null)
                        throw new HubconGenericException("No se recibió ningun mensaje del servidor.");

                    var operationResponse = converter.DeserializeJsonElement<BaseOperationResponse<T>>(result)
                        ?? throw new HubconGenericException("No se recibió ningun mensaje del servidor.");

                    if (!operationResponse.Success)
                        throw new HubconRemoteException($"Ocurrió un error en el servidor. Mensaje recibido: {operationResponse.Error}");

                    await CallHook(operationOptions, HookType.OnResponse, ServiceProvider, request, cancellationToken, operationResponse.Data);
                    await CallHook(contractOptions, HookType.OnResponse, ServiceProvider, request, cancellationToken, operationResponse.Data);

                    return operationResponse.Data;
                }
            }
            catch (Exception ex)
            {
                await CallHook(operationOptions, HookType.OnError, ServiceProvider, request, cancellationToken, null, ex);
                await CallHook(contractOptions, HookType.OnError, ServiceProvider, request, cancellationToken, null, ex);

                if (ex is OperationCanceledException)
                    throw;
                if (ex is HubconRemoteException)
                    throw;
                else if (ex is HubconGenericException)
                    throw;
                else
                    throw new HubconGenericException(ex.Message, ex);
            }

        }

        public async Task CallAsync(IOperationRequest request, MethodInfo methodInfo, CancellationToken cancellationToken)
        {
            if (!IsBuilt)
                throw new InvalidOperationException("El cliente no ha sido construido. Asegúrese de llamar a 'Build()' antes de usar este método.");

            IOperationOptions? operationOptions = null;
            IContractOptions? contractOptions = null;

            bool isWebsocketOperation = false;

            if (ContractOptionsDict!.TryGetValue(methodInfo.ReflectedType!, out contractOptions))
            {
                isWebsocketOperation = contractOptions.IsWebsocketOperation(request.OperationName);
                operationOptions = contractOptions.GetOperationOptions(request.OperationName);
            }
            
            bool remoteCancellation = operationOptions?.RemoteCancellationIsAllowed 
                                      ?? contractOptions?.RemoteCancellationIsAllowed 
                                      ?? false;

            await CallValidationHook(operationOptions, ServiceProvider, request, cancellationToken);

            try
            {
                if (isWebsocketOperation)
                {
                    await RateLimiterHelper.AcquireAsync(clientOptions, clientOptions?.RateBucket, clientOptions?.WebsocketFireAndForgetRateBucket, operationOptions?.RateBucket);
                    
                    await CallHook(operationOptions, HookType.OnSend, ServiceProvider, request, cancellationToken);
                    await CallHook(contractOptions, HookType.OnSend, ServiceProvider, request, cancellationToken);

                    await client.SendAsync(request, remoteCancellation, cancellationToken);

                    await CallHook(operationOptions, HookType.OnAfterSend, ServiceProvider, request, cancellationToken);
                    await CallHook(contractOptions, HookType.OnAfterSend, ServiceProvider, request, cancellationToken);
                }
                else
                {
                    await RateLimiterHelper.AcquireAsync(clientOptions, clientOptions?.RateBucket, clientOptions?.HttpFireAndForgetRateBucket, operationOptions?.RateBucket);

                    var arguments = converter.Serialize(request.Arguments);
                    using var content = new StringContent(arguments, Encoding.UTF8, "application/json");

                    HttpMethod httpMethod = request.Arguments!.Any() ? HttpMethod.Post : HttpMethod.Get;

                    var url = _restHttpUrl + methodInfo.GetRoute();
                    var httpRequest = new HttpRequestMessage(httpMethod, url)
                    {
                        Content = content
                    };
                    
                    if (AuthenticationManager.IsSessionActive)
                        httpRequest.Headers.Authorization = new AuthenticationHeaderValue(AuthenticationManager.TokenType!, AuthenticationManager.AccessToken);

                    await CallHook(operationOptions, HookType.OnSend, ServiceProvider, request, cancellationToken);
                    await CallHook(contractOptions, HookType.OnSend, ServiceProvider, request, cancellationToken);

                    await HttpClient.SendAsync(httpRequest, cancellationToken);

                    await CallHook(operationOptions, HookType.OnAfterSend, ServiceProvider, request, cancellationToken);
                    await CallHook(contractOptions, HookType.OnAfterSend, ServiceProvider, request, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                await CallHook(operationOptions, HookType.OnError, ServiceProvider, request, cancellationToken, null, ex);
                await CallHook(contractOptions, HookType.OnError, ServiceProvider, request, cancellationToken, null, ex);

                if (ex is OperationCanceledException)
                    throw;
                else if (ex is HubconRemoteException)
                    throw;
                else if (ex is HubconGenericException)
                    throw;
                else
                    throw new HubconGenericException(ex.Message, ex);
            }
        }

        public async IAsyncEnumerable<JsonElement> GetStream(IOperationRequest request, MethodInfo method, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            IOperationOptions? operationOptions = null;

            if (ContractOptionsDict!.TryGetValue(method.ReflectedType!, out IContractOptions? contractOptions))
            {
                operationOptions = contractOptions.GetOperationOptions(request.OperationName);
            }
            
            bool remoteCancellation = operationOptions?.RemoteCancellationIsAllowed 
                                      ?? contractOptions?.RemoteCancellationIsAllowed 
                                      ?? false;

            IObservable<JsonElement> observable;

            await CallValidationHook(operationOptions, ServiceProvider, request, cancellationToken);

            try
            {
                await RateLimiterHelper.AcquireAsync(clientOptions, clientOptions?.RateBucket, clientOptions?.StreamingRateBucket, operationOptions?.RateBucket);

                await CallHook(operationOptions, HookType.OnSend, ServiceProvider, request, cancellationToken);
                await CallHook(contractOptions, HookType.OnSend, ServiceProvider, request, cancellationToken);

                observable = await client.Stream<JsonElement>(request, remoteCancellation, cancellationToken);

                await CallHook(operationOptions, HookType.OnAfterSend, ServiceProvider, request, cancellationToken);
                await CallHook(contractOptions, HookType.OnAfterSend, ServiceProvider, request, cancellationToken);
            }
            catch (Exception ex)
            {
                await CallHook(operationOptions, HookType.OnError, ServiceProvider, request, cancellationToken);
                await CallHook(contractOptions, HookType.OnError, ServiceProvider, request, cancellationToken);

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
                var enumerator = observer.GetAsyncEnumerable(cancellationToken).GetAsyncEnumerator(cancellationToken);

                if (operationOptions != null)
                    await operationOptions.CallHook(HookType.OnSubscribed, ServiceProvider, request, cancellationToken);

                while (true)
                {
                    JsonElement result = default;

                    try
                    {
                        if (!await enumerator.MoveNextAsync())
                            break;

                        await RateLimiterHelper.AcquireAsync(clientOptions, clientOptions?.RateBucket, clientOptions?.StreamingRateBucket, operationOptions?.RateBucket);

                        result = enumerator.Current;
                    }
                    catch (Exception ex)
                    {
                        await CallHook(operationOptions, HookType.OnError, ServiceProvider, request, cancellationToken);
                        await CallHook(contractOptions, HookType.OnError, ServiceProvider, request, cancellationToken);

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

            await CallHook(operationOptions, HookType.OnUnsubscribed, ServiceProvider, request, cancellationToken);
            await CallHook(contractOptions, HookType.OnUnsubscribed, ServiceProvider, request, cancellationToken);
        }

        public async Task<T> Ingest<T>(IOperationRequest request, MethodInfo method, CancellationToken cancellationToken)
        {
            IOperationOptions? operationOptions = null;
            IContractOptions? contractOptions = null;

            if (ContractOptionsDict!.TryGetValue(method.ReflectedType!, out contractOptions))
            {
                operationOptions = contractOptions.GetOperationOptions(request.OperationName);
            }

            bool remoteCancellation = operationOptions?.RemoteCancellationIsAllowed 
                                      ?? contractOptions?.RemoteCancellationIsAllowed 
                                      ?? false;
            
            await CallValidationHook(operationOptions, ServiceProvider, request, cancellationToken);

            try 
            {
                await RateLimiterHelper.AcquireAsync(clientOptions, clientOptions?.RateBucket, clientOptions?.IngestRateBucket, operationOptions?.RateBucket);

                await CallHook(operationOptions, HookType.OnSend, ServiceProvider, request, cancellationToken);
                await CallHook(contractOptions, HookType.OnSend, ServiceProvider, request, cancellationToken);

                var response = await client.IngestMultiple<T>(request, remoteCancellation, clientOptions, operationOptions, cancellationToken);

                await CallHook(operationOptions, HookType.OnAfterSend, ServiceProvider, request, cancellationToken);
                await CallHook(contractOptions, HookType.OnAfterSend, ServiceProvider, request, cancellationToken);

                await CallHook(operationOptions, HookType.OnResponse, ServiceProvider, request, cancellationToken);
                await CallHook(contractOptions, HookType.OnResponse, ServiceProvider, request, cancellationToken);

                return response.Data;
            }
            catch (Exception ex)
            {
                await CallHook(operationOptions, HookType.OnError, ServiceProvider, request, cancellationToken);
                await CallHook(contractOptions, HookType.OnError, ServiceProvider, request, cancellationToken);

                if (ex is HubconRemoteException)
                    throw;
                else if (ex is HubconGenericException)
                    throw;
                else
                    throw;
            }
        }

        public async Task<IAsyncEnumerable<JsonElement>> GetSubscription(IOperationRequest request, MemberInfo method, CancellationToken cancellationToken = default)
        {
            IOperationOptions? operationOptions = null;
            IContractOptions? contractOptions = null;

            if (ContractOptionsDict!.TryGetValue(method.ReflectedType!, out contractOptions))
            {
                operationOptions = contractOptions.GetOperationOptions(request.OperationName);
            }
            
            bool remoteCancellation = operationOptions?.RemoteCancellationIsAllowed 
                                      ?? contractOptions?.RemoteCancellationIsAllowed 
                                      ?? false;

            await CallValidationHook(operationOptions, ServiceProvider, request, cancellationToken);

            try
            {
                return HandleSubscription(request, remoteCancellation, method, contractOptions, operationOptions, cancellationToken);
            }
            catch (Exception ex)
            {
                await CallHook(operationOptions, HookType.OnError, ServiceProvider, request, cancellationToken);
                await CallHook(contractOptions, HookType.OnError, ServiceProvider, request, cancellationToken);

                if (ex is HubconRemoteException)
                    throw;
                else if (ex is HubconGenericException)
                    throw;
                else
                    throw new HubconGenericException(ex.Message, ex);
            }

        }

        private async IAsyncEnumerable<JsonElement> HandleSubscription(
            IOperationRequest request,
            bool remoteCancellation, 
            MemberInfo method, 
            IContractOptions? contractOptions,
            IOperationOptions? operationOptions, 
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await RateLimiterHelper.AcquireAsync(clientOptions, clientOptions?.RateBucket, clientOptions?.SubscriptionRateBucket, operationOptions?.RateBucket);

            IObservable<JsonElement> observable;

            try
            {
                await CallHook(operationOptions, HookType.OnSend, ServiceProvider, request, cancellationToken);
                await CallHook(contractOptions, HookType.OnSend, ServiceProvider, request, cancellationToken);

                observable = await client.Subscribe<JsonElement>(request, remoteCancellation);

                await CallHook(operationOptions, HookType.OnAfterSend, ServiceProvider, request, cancellationToken);
                await CallHook(contractOptions, HookType.OnAfterSend, ServiceProvider, request, cancellationToken);
            }
            catch (Exception ex)
            {
                await CallHook(operationOptions, HookType.OnError, ServiceProvider, request, cancellationToken, null, ex);
                await CallHook(contractOptions, HookType.OnError, ServiceProvider, request, cancellationToken, null, ex);

                throw new HubconGenericException($"Error al obtener el stream del servidor. Mensaje: {ex.Message}", ex);
            }

            var options = new BoundedChannelOptions(5000);

            var observer = AsyncObserver.Create<JsonElement>(converter, options);

            try
            {
                using (observable.Subscribe(observer))
                {
                    await CallHook(operationOptions, HookType.OnSubscribed, ServiceProvider, request, cancellationToken);
                    await CallHook(contractOptions, HookType.OnSubscribed, ServiceProvider, request, cancellationToken);

                    var enumerator = observer.GetAsyncEnumerable(cancellationToken).GetAsyncEnumerator();

                    while (true)
                    {
                        JsonElement result = default;

                        try
                        {
                            if (!await enumerator.MoveNextAsync())
                                break;

                            await RateLimiterHelper.AcquireAsync(clientOptions, clientOptions?.RateBucket, clientOptions?.SubscriptionRateBucket, operationOptions?.RateBucket);

                            result = enumerator.Current;

                            await CallHook(operationOptions, HookType.OnEventReceived, ServiceProvider, request, cancellationToken, result, null);
                            await CallHook(contractOptions, HookType.OnEventReceived, ServiceProvider, request, cancellationToken, result, null);
                        }
                        catch (Exception ex)
                        {
                            await CallHook(operationOptions, HookType.OnError, ServiceProvider, request, cancellationToken, null, ex);
                            await CallHook(contractOptions, HookType.OnError, ServiceProvider, request, cancellationToken, null, ex);

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

                await CallHook(operationOptions, HookType.OnUnsubscribed, ServiceProvider, request, cancellationToken);
                await CallHook(contractOptions, HookType.OnUnsubscribed, ServiceProvider, request, cancellationToken);
            }
        }

        private static Task CallHook(IContractOptions? options, HookType type, IServiceProvider services, IOperationRequest request, CancellationToken cancellationToken, object? result = null, Exception? exception = null)
        {
            return options == null ? Task.CompletedTask : options.CallHook(type, services, request, cancellationToken, result, exception);
        }

        private static Task CallHook(IOperationOptions? options, HookType type, IServiceProvider services, IOperationRequest request, CancellationToken cancellationToken, object? result = null, Exception? exception = null)
        {
            return options != null ? options.CallHook(type, services, request, cancellationToken, result, exception) : Task.CompletedTask;
        }

        private static Task CallValidationHook(IOperationOptions? options, IServiceProvider services, IOperationRequest request, CancellationToken cancellationToken)
        {
            return options == null ? Task.CompletedTask : options.CallValidationHook(services, request, cancellationToken);
        }

        public void Build(
            IClientOptions options,
            IServiceProvider serviceProvider,
            IDictionary<Type, IContractOptions> contractOptions,
            bool useSecureConnection = true)
        {
            if (IsBuilt) return;

            var baseUri = options.BaseUri;
            var httpEndpoint = options.HttpPrefix;
            var websocketEndpoint = options.WebsocketPrefix;
            var authenticationManagerType = options.AuthenticationManagerType;

            ContractOptionsDict ??= contractOptions;

            var baseRestHttpUrl = $"{baseUri!.AbsoluteUri}/{httpEndpoint ?? ""}".TrimEnd('/');
            var baseRestWebsocketUrl = $"{baseUri!.AbsoluteUri}/{websocketEndpoint ?? "ws"}".TrimEnd('/');

            _restHttpUrl = useSecureConnection ? $"https://{baseRestHttpUrl}" : $"http://{baseRestHttpUrl}";
            _websocketUrl = useSecureConnection ? $"wss://{baseRestWebsocketUrl}" : $"ws://{baseRestWebsocketUrl}";
            
            if (authenticationManagerType is not null)
            {
                var lazyAuthType = typeof(Lazy<>).MakeGenericType(authenticationManagerType);
                authenticationManagerFactory = () => (IAuthenticationManager)((dynamic)serviceProvider.GetRequiredService(lazyAuthType)).Value;
            }

            client = new HubconWebSocketClient(new Uri(_websocketUrl), converter, options, serviceProvider, serviceProvider.GetService<ILogger<HubconWebSocketClient>>());

            client.AuthorizationTokenProvider = () => AuthenticationManager.AccessToken;

            client.WebSocketOptions = options.WebSocketOptions;

            this.ServiceProvider = serviceProvider;

            clientOptions = options;

            IsBuilt = true;
        }
    }
}
