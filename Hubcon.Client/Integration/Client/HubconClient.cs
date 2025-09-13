using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Client.Core.Exceptions;
using Hubcon.Client.Core.Helpers;
using Hubcon.Client.Core.Websockets;
using Hubcon.Shared.Abstractions.Attributes;
using Hubcon.Shared.Abstractions.Enums;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Core.Extensions;
using Hubcon.Shared.Core.Websockets.Events;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reactive.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace Hubcon.Client.Integration.Client
{
    internal sealed class HubconClient(IDynamicConverter converter, IHttpClientFactory clientFactory) : IHubconClient
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

        //private IDictionary<Type, IContractOptions> ContractOptionsDict { get; set; } = null!;

        private ConcurrentDictionary<MethodInfo, bool> NeedsAuth = new();

        private ConcurrentDictionary<MethodInfo, HttpMethod> MethodVerb = new();

        private ConcurrentDictionary<MethodInfo, bool> ShouldUseBody = new();

        public async Task<T> SendAsync<T>(IOperationRequest request, MethodInfo methodInfo, CancellationToken cancellationToken)
        {           
            var contractOptions = clientOptions.GetContractOptions(methodInfo.ReflectedType!);
            IOperationOptions operationOptions = contractOptions!.GetOperationOptions(request.OperationName, methodInfo)!;

            bool isWebsocketMethod = contractOptions.IsWebsocketOperation(request.OperationName);

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
                    await RateLimiterHelper.AcquireAsync(clientOptions, clientOptions?.RateBucket, clientOptions?.HttpFireAndForgetRateBucket, operationOptions?.RateBucket);

                    HttpMethod httpMethod = MethodVerb.GetOrAdd(methodInfo, method =>
                    {
                        GetMethodAttribute? verb = method.GetCustomAttribute<GetMethodAttribute>();
                        return verb != null ? HttpMethod.Get : (request.Arguments.Count > 0 ? HttpMethod.Post : HttpMethod.Get);
                    });

                    StringContent? content = null;
                    var url = "";
                    var shouldUseBody = ShouldUseBody.GetOrAdd(methodInfo, method =>
                    {
                        var parameters = method.GetParameters();
                        bool shouldUseBodyParameters = true;

                        foreach (var parameter in parameters)
                        {
                            shouldUseBodyParameters &= ShouldBindFromBody(parameter.ParameterType);
                        }

                        return shouldUseBodyParameters;
                    });

                    if (shouldUseBody)
                    {
                        var arguments = converter.Serialize(request.Arguments);
                        content = new StringContent(arguments, Encoding.UTF8, "application/json");
                        url = _restHttpUrl + methodInfo.GetRoute().FullRoute;
                    }
                    else
                    {
                        var builder = new UriBuilder(_restHttpUrl);

                        var query = System.Web.HttpUtility.ParseQueryString(builder.Query);

                        foreach (var argument in request.Arguments)
                        {
                            query[argument.Key] = argument.Value?.ToString() ?? "";
                        }

                        builder.Path = methodInfo.GetRoute().FullRoute;
                        builder.Query = query.ToString();
                        url = builder.ToString();
                    }

                    var httpRequest = new HttpRequestMessage(httpMethod, url);

                    if (content != null)
                        httpRequest.Content = content;

                    bool needsAuth = NeedsAuth.GetOrAdd(methodInfo, _ =>
                    {
                        return (operationOptions?.HttpAuthIsEnabled ?? true)
                            && (contractOptions?.HttpAuthIsEnabled ?? true)
                            && clientOptions!.HttpAuthIsEnabled;
                    });

                    if (needsAuth && AuthenticationManager.IsSessionActive)
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
                    
                    content?.Dispose();

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

            var contractOptions = clientOptions.GetContractOptions(methodInfo.ReflectedType!);
            IOperationOptions? operationOptions = contractOptions!.GetOperationOptions(request.OperationName, methodInfo);

            bool isWebsocketOperation = contractOptions.IsWebsocketOperation(request.OperationName);
            
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

                    HttpMethod httpMethod = MethodVerb.GetOrAdd(methodInfo, method =>
                    {
                        GetMethodAttribute? verb = method.GetCustomAttribute<GetMethodAttribute>();
                        return verb != null ? HttpMethod.Get : (request.Arguments.Count > 0 ? HttpMethod.Post : HttpMethod.Get);
                    });

                    StringContent? content = null;
                    var url = "";
                    var shouldUseBody = ShouldUseBody.GetOrAdd(methodInfo, method =>
                    {
                        var parameters = method.GetParameters();
                        bool shouldUseBodyParameters = true;

                        foreach (var parameter in parameters)
                        {
                            shouldUseBodyParameters &= ShouldBindFromBody(parameter.ParameterType);
                        }

                        return shouldUseBodyParameters;
                    });

                    if (shouldUseBody)
                    {
                        var arguments = converter.Serialize(request.Arguments);
                        content = new StringContent(arguments, Encoding.UTF8, "application/json");
                        url = _restHttpUrl + methodInfo.GetRoute().FullRoute;
                    }
                    else
                    {
                        var builder = new UriBuilder(_restHttpUrl);

                        var query = System.Web.HttpUtility.ParseQueryString(builder.Query);

                        foreach (var argument in request.Arguments)
                        {
                            query[argument.Key] = argument.Value?.ToString() ?? "";
                        }

                        builder.Path = methodInfo.GetRoute().FullRoute;
                        builder.Query = query.ToString();
                        url = builder.ToString();
                    }

                    url += methodInfo.GetRoute().FullRoute;
                    var httpRequest = new HttpRequestMessage(httpMethod, url);

                    if(content != null) 
                        httpRequest.Content = content;

                    bool needsAuth = NeedsAuth.GetOrAdd(methodInfo, _ =>
                    {
                        return (operationOptions?.HttpAuthIsEnabled ?? true)
                            && (contractOptions?.HttpAuthIsEnabled ?? true)
                            && clientOptions!.HttpAuthIsEnabled;
                    });

                    if (needsAuth && AuthenticationManager.IsSessionActive)
                        httpRequest.Headers.Authorization = new AuthenticationHeaderValue(AuthenticationManager.TokenType!, AuthenticationManager.AccessToken);

                    await CallHook(operationOptions, HookType.OnSend, ServiceProvider, request, cancellationToken);
                    await CallHook(contractOptions, HookType.OnSend, ServiceProvider, request, cancellationToken);
                    
                    await HttpClient.SendAsync(httpRequest, cancellationToken);
                    
                    await CallHook(operationOptions, HookType.OnAfterSend, ServiceProvider, request, cancellationToken);
                    await CallHook(contractOptions, HookType.OnAfterSend, ServiceProvider, request, cancellationToken);

                    content?.Dispose();
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
            var contractOptions = clientOptions.GetContractOptions(method.ReflectedType!);
            IOperationOptions? operationOptions = contractOptions!.GetOperationOptions(request.OperationName, method);

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
            var contractOptions = clientOptions.GetContractOptions(method.ReflectedType!);
            IOperationOptions? operationOptions = contractOptions!.GetOperationOptions(request.OperationName, method);

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
            var contractOptions = clientOptions.GetContractOptions(method.ReflectedType!);
            IOperationOptions? operationOptions = contractOptions!.GetOperationOptions(request.OperationName, method);

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

        // Devuelve true si el parámetro debería ir al body, false si va a query
        public static bool ShouldBindFromBody(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));

            // Nullable<T> → revisar T subyacente
            if (Nullable.GetUnderlyingType(type) is Type underlying)
                type = underlying;

            // Tipos primitivos / simples → query
            if (type.IsPrimitive
                || type.IsEnum
                || type == typeof(string)
                || type == typeof(decimal)
                || type == typeof(Guid)
                || type == typeof(DateTime)
                || type == typeof(DateTimeOffset)
                || type == typeof(TimeSpan))
            {
                return false; // bindear de query
            }

            // IEnumerable de tipo simple → normalmente se toma de query como array
            if (typeof(IEnumerable<>).IsAssignableFrom(type) && type != typeof(string))
            {
                return false;
            }

            // Todo lo demás → body
            return true;
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

            //ContractOptionsDict ??= contractOptions;

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

            client.LoggingEnabled = options.LoggingEnabled;

            this.ServiceProvider = serviceProvider;

            clientOptions = options;

            IsBuilt = true;
        }
    }
}
