using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Client.Core.Exceptions;
using Hubcon.Client.Core.Helpers;
using Hubcon.Client.Core.Websockets;
using Hubcon.Shared.Abstractions.Attributes;
using Hubcon.Shared.Abstractions.Enums;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Abstractions.Standard.Extensions;
using Hubcon.Shared.Core.Extensions;
using Hubcon.Shared.Core.Lazy;
using Hubcon.Shared.Core.Websockets.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Hubcon.Client.Integration.Client
{
    internal sealed class HubconClient : IHubconClient
    {
        private string _restHttpUrl = "";
        private string _websocketUrl = "";

        Func<IAuthenticationManager?>? authenticationManagerFactory;

        IAuthenticationManager? _authenticationManager;
        IAuthenticationManager AuthenticationManager
        {
            get
            {
                var manager = _authenticationManager 
                    ??= authenticationManagerFactory?.Invoke() 
                    ?? throw new InvalidOperationException($"Authentication Manager not defined for server module '{ClientOptions.ServerModuleName}'.");

                manager.OnSessionIsInactive += async () =>
                {
                    await client.Disconnect();
                };
                
                return manager!;
            }
        }

        HubconWebSocketClient client = null!;

        HttpClient? _httpClient;
        HttpClient HttpClient
        {
            get
            {
                if (_httpClient != null)
                    return _httpClient;

                _httpClient ??= clientFactory.CreateClient();
                ClientOptions.HttpClientOptions?.Invoke(_httpClient, ServiceProvider);

                return _httpClient;
            }
        }

        private IServiceProvider ServiceProvider { get; set; } = null!;

        private IClientOptions ClientOptions { get; set; } = null!;

        private bool IsBuilt { get; set; }

        //private IDictionary<Type, IContractOptions> ContractOptionsDict { get; set; } = null!;

        private ConcurrentDictionary<MethodInfo, bool> NeedsAuth = new ConcurrentDictionary<MethodInfo, bool>();

        private ConcurrentDictionary<MethodInfo, HttpMethod> MethodVerb = new ConcurrentDictionary<MethodInfo, HttpMethod>();

        private ConcurrentDictionary<MethodInfo, bool> ShouldUseBody = new ConcurrentDictionary<MethodInfo, bool>();
        private readonly IDynamicConverter converter;
        private readonly IHttpClientFactory clientFactory;

        public HubconClient(IDynamicConverter converter, IHttpClientFactory clientFactory)
        {
            this.converter = converter;
            this.clientFactory = clientFactory;
        }

        public async Task<T> SendAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(IOperationRequest request, MethodInfo methodInfo, CancellationToken cancellationToken)
        {
            var context = new InvocationContext()
            {
                Services = ServiceProvider,
                CancellationToken = cancellationToken,
                Request = request,
                TryRefreshToken = client.TryRefreshToken
            };

            IContractOptions contractOptions = ClientOptions.GetContractOptions(methodInfo.DeclaringType!);
            IOperationOptions operationOptions = contractOptions.GetOperationOptions(request.OperationName, methodInfo);

            bool isWebsocketMethod = contractOptions.IsWebsocketOperation(request.OperationName);

            bool remoteCancellation = operationOptions.RemoteCancellationIsAllowed ?? contractOptions.RemoteCancellationIsAllowed;

            await operationOptions.CallValidationHook(ServiceProvider, request, cancellationToken);

            try
            {
                if (isWebsocketMethod)
                {
                    await RateLimiterHelper.AcquireAsync(ClientOptions, ClientOptions.RateBucket, ClientOptions.WebsocketRoundTripRateBucket, operationOptions.RateBucket);

                    await operationOptions.CallHook(HookType.OnSend, context);
                    await contractOptions.CallHook(HookType.OnSend, context);
                    await ClientOptions.CallInterceptor(InterceptorType.OnSend, context);

                    if (ClientOptions.UseHttpEndpointOverloading)
                        request.SetOperationName(methodInfo.GetMethodSignature(true));

                    var result = await client.InvokeAsync<JsonElement>(request, remoteCancellation, cancellationToken);

                    await operationOptions.CallHook(HookType.OnAfterSend, context);
                    await contractOptions.CallHook(HookType.OnAfterSend, context);
                    await ClientOptions.CallInterceptor(InterceptorType.OnAfterSend, context);

                    if (!result.Success)
                        throw new HubconRemoteException($"Ocurrió un error en el servidor. Mensaje recibido: {result.Error}");

                    await operationOptions.CallHook(HookType.OnResponse, context);
                    await contractOptions.CallHook(HookType.OnResponse, context);
                    await ClientOptions.CallInterceptor(InterceptorType.OnResponse, context);

                    return converter.DeserializeJsonElement<T>(result.Data!);
                }
                else
                {
                    await RateLimiterHelper.AcquireAsync(ClientOptions, ClientOptions.RateBucket, ClientOptions.HttpFireAndForgetRateBucket, operationOptions.RateBucket);

                    HttpMethod httpMethod = MethodVerb.GetOrAdd(methodInfo, method =>
                    {
                        GetMethodAttribute? verb = method.GetCustomAttribute<GetMethodAttribute>();
                        return verb != null ? HttpMethod.Get : (request.Arguments.Count > 0 ? HttpMethod.Post : HttpMethod.Get);
                    });

                    StringContent? content = null;
                    var url = "";

                    if (httpMethod == HttpMethod.Post)
                    {
                        var arguments = converter.Serialize(request.Arguments);
                        content = new StringContent(arguments, Encoding.UTF8, "application/json");
                        url = _restHttpUrl + methodInfo.GetRoute(ClientOptions.UseHttpEndpointOverloading).FullRoute;
                    }
                    else
                    {
                        var builder = new UriBuilder(_restHttpUrl);

                        var query = System.Web.HttpUtility.ParseQueryString(builder.Query);

                        foreach (var argument in request.Arguments)
                        {
                            query[argument.Key] = argument.Value?.ToString() ?? "";
                        }

                        builder.Path = methodInfo.GetRoute(ClientOptions.UseHttpEndpointOverloading).FullRoute;
                        builder.Query = query.ToString();
                        url = builder.ToString();
                    }

                    var httpRequest = new HttpRequestMessage(httpMethod, url);

                    if (content != null)
                        httpRequest.Content = content;

                    bool needsAuth = NeedsAuth.GetOrAdd(methodInfo, _ =>
                    {
                        return (operationOptions.HttpAuthIsEnabled ?? true)
                            && (contractOptions.HttpAuthIsEnabled)
                            && ClientOptions.HttpAuthIsEnabled;
                    });

                    if (needsAuth && AuthenticationManager.IsSessionActive)
                        httpRequest.Headers.Authorization = new AuthenticationHeaderValue(AuthenticationManager.TokenType!, AuthenticationManager.AccessToken);

                    await operationOptions.CallHook(HookType.OnSend, context);
                    await contractOptions.CallHook(HookType.OnSend, context);
                    await ClientOptions.CallInterceptor(InterceptorType.OnSend, context);

                    HttpResponseMessage response = await HttpClient.SendAsync(httpRequest, cancellationToken);

                    await operationOptions.CallHook(HookType.OnAfterSend, context);
                    await contractOptions.CallHook(HookType.OnAfterSend, context);
                    await ClientOptions.CallInterceptor(InterceptorType.OnAfterSend, context);

                    var responseBytes = await response.Content.ReadAsByteArrayAsync();
                    var result = converter.DeserializeByteArray<JsonElement>(responseBytes);

                    if (result.ValueKind == JsonValueKind.Null)
                        throw new HubconGenericException("No se recibió ningun mensaje del servidor.");

                    var operationResponse = converter.DeserializeJsonElement<BaseOperationResponse<JsonElement>>(result)
                        ?? throw new HubconGenericException("No se recibió ningun mensaje del servidor.");

                    context.IsSuccess = operationResponse.Success;
                    context.Result = operationResponse.Data;
                    context.Error = operationResponse.Error;

                    if (!operationResponse.Success)
                        throw new HubconRemoteException($"Ocurrió un error en el servidor. Mensaje recibido: {operationResponse.Error}");


                    await operationOptions.CallHook(HookType.OnResponse, context);
                    await contractOptions.CallHook(HookType.OnResponse, context);
                    await ClientOptions.CallInterceptor(InterceptorType.OnResponse, context);

                    content?.Dispose();
                    return converter.DeserializeJsonElement<T>(operationResponse.Data!);
                }
            }
            catch (Exception ex)
            {
                context.IsSuccess = false;
                context.Exception = ex;

                await operationOptions.CallHook(HookType.OnError, context);
                await contractOptions.CallHook(HookType.OnError, context);
                await ClientOptions.CallInterceptor(InterceptorType.OnError, context);

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

            var context = new InvocationContext()
            {
                Services = ServiceProvider,
                CancellationToken = cancellationToken,
                Request = request,
                TryRefreshToken = client.TryRefreshToken
            };

            IContractOptions contractOptions = ClientOptions.GetContractOptions(methodInfo.ReflectedType!);
            IOperationOptions operationOptions = contractOptions.GetOperationOptions(request.OperationName, methodInfo);

            bool isWebsocketOperation = contractOptions.IsWebsocketOperation(request.OperationName);

            bool remoteCancellation = operationOptions.RemoteCancellationIsAllowed ?? contractOptions.RemoteCancellationIsAllowed;

            await operationOptions.CallValidationHook(ServiceProvider, request, cancellationToken);

            try
            {
                if (isWebsocketOperation)
                {
                    await RateLimiterHelper.AcquireAsync(ClientOptions, ClientOptions.RateBucket, ClientOptions.WebsocketFireAndForgetRateBucket, operationOptions.RateBucket);

                    await operationOptions.CallHook(HookType.OnSend, context);
                    await contractOptions.CallHook(HookType.OnSend, context);
                    await ClientOptions.CallInterceptor(InterceptorType.OnSend, context);

                    if (ClientOptions.UseHttpEndpointOverloading)
                        request.SetOperationName(methodInfo.GetMethodSignature(true));

                    await client.SendAsync(request, remoteCancellation, cancellationToken);

                    await operationOptions.CallHook(HookType.OnAfterSend, context);
                    await contractOptions.CallHook(HookType.OnAfterSend, context);
                    await ClientOptions.CallInterceptor(InterceptorType.OnAfterSend, context);
                }
                else
                {
                    await RateLimiterHelper.AcquireAsync(ClientOptions, ClientOptions.RateBucket, ClientOptions.HttpFireAndForgetRateBucket, operationOptions.RateBucket);

                    HttpMethod httpMethod = MethodVerb.GetOrAdd(methodInfo, method =>
                    {
                        GetMethodAttribute? verb = method.GetCustomAttribute<GetMethodAttribute>();
                        return verb != null ? HttpMethod.Get : (request.Arguments.Count > 0 ? HttpMethod.Post : HttpMethod.Get);
                    });

                    StringContent? content = null;
                    var url = "";

                    if (httpMethod == HttpMethod.Post)
                    {
                        var arguments = converter.Serialize(request.Arguments);
                        content = new StringContent(arguments, Encoding.UTF8, "application/json");
                        url = _restHttpUrl + methodInfo.GetRoute(ClientOptions.UseHttpEndpointOverloading).FullRoute;
                    }
                    else
                    {
                        var builder = new UriBuilder(_restHttpUrl);

                        var query = System.Web.HttpUtility.ParseQueryString(builder.Query);

                        foreach (var argument in request.Arguments)
                        {
                            query[argument.Key] = argument.Value?.ToString() ?? "";
                        }

                        builder.Path = methodInfo.GetRoute(ClientOptions.UseHttpEndpointOverloading).FullRoute;
                        builder.Query = query.ToString();
                        url = builder.ToString();
                    }

                    url += methodInfo.GetRoute(ClientOptions.UseHttpEndpointOverloading).FullRoute;
                    var httpRequest = new HttpRequestMessage(httpMethod, url);

                    if (content != null)
                        httpRequest.Content = content;

                    bool needsAuth = NeedsAuth.GetOrAdd(methodInfo, _ =>
                    {
                        return (operationOptions.HttpAuthIsEnabled ?? true)
                            && contractOptions.HttpAuthIsEnabled
                            && ClientOptions.HttpAuthIsEnabled;
                    });

                    if (needsAuth && AuthenticationManager.IsSessionActive)
                        httpRequest.Headers.Authorization = new AuthenticationHeaderValue(AuthenticationManager.TokenType!, AuthenticationManager.AccessToken);

                    await operationOptions.CallHook(HookType.OnSend, context);
                    await contractOptions.CallHook(HookType.OnSend, context);
                    await ClientOptions.CallInterceptor(InterceptorType.OnSend, context);

                    await HttpClient.SendAsync(httpRequest, cancellationToken);

                    await operationOptions.CallHook(HookType.OnAfterSend, context);
                    await contractOptions.CallHook(HookType.OnAfterSend, context);
                    await ClientOptions.CallInterceptor(InterceptorType.OnAfterSend, context);

                    content?.Dispose();
                }
            }
            catch (Exception ex)
            {
                context.Exception = ex;
                context.IsSuccess = false;

                await operationOptions.CallHook(HookType.OnError, context);
                await contractOptions.CallHook(HookType.OnError, context);
                await ClientOptions.CallInterceptor(InterceptorType.OnError, context);

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
            var context = new InvocationContext()
            {
                Services = ServiceProvider,
                CancellationToken = cancellationToken,
                Request = request,
                TryRefreshToken = client.TryRefreshToken
            };

            IContractOptions contractOptions = ClientOptions.GetContractOptions(method.ReflectedType!);
            IOperationOptions operationOptions = contractOptions!.GetOperationOptions(request.OperationName, method);

            bool remoteCancellation = operationOptions.RemoteCancellationIsAllowed ?? contractOptions.RemoteCancellationIsAllowed;

            IObservable<JsonElement> observable;

            await operationOptions.CallValidationHook(ServiceProvider, request, cancellationToken);

            try
            {
                await RateLimiterHelper.AcquireAsync(ClientOptions, ClientOptions.RateBucket, ClientOptions.StreamingRateBucket, operationOptions.RateBucket);

                await operationOptions.CallHook(HookType.OnSend, context);
                await contractOptions.CallHook(HookType.OnSend, context);
                await ClientOptions.CallInterceptor(InterceptorType.OnSend, context);

                if (ClientOptions.UseHttpEndpointOverloading)
                    request.SetOperationName(method.GetMethodSignature(true));

                observable = await client.Stream<JsonElement>(request, remoteCancellation, cancellationToken);

                await operationOptions.CallHook(HookType.OnAfterSend, context);
                await contractOptions.CallHook(HookType.OnAfterSend, context);
                await ClientOptions.CallInterceptor(InterceptorType.OnAfterSend, context);
            }
            catch (Exception ex)
            {
                context.Exception = ex;
                context.IsSuccess = false;

                await operationOptions.CallHook(HookType.OnError, context);
                await contractOptions.CallHook(HookType.OnError, context);
                await ClientOptions.CallInterceptor(InterceptorType.OnError, context);

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

                await operationOptions.CallHook(HookType.OnSubscribed, context);
                await contractOptions.CallHook(HookType.OnSubscribed, context);
                await ClientOptions.CallInterceptor(InterceptorType.OnSubscribed, context);

                while (true)
                {
                    JsonElement result = default;
                    try
                    {
                        if (!await enumerator.MoveNextAsync())
                            break;

                        await RateLimiterHelper.AcquireAsync(ClientOptions, ClientOptions.RateBucket, ClientOptions.StreamingRateBucket, operationOptions.RateBucket);

                        result = enumerator.Current;
                        context.IsSuccess = true;
                        context.Result = result;
                    }
                    catch (Exception ex)
                    {
                        context.IsSuccess = false;
                        context.Result = result;
                        context.Exception = ex;

                        await operationOptions.CallHook(HookType.OnError, context);
                        await contractOptions.CallHook(HookType.OnError, context);
                        await ClientOptions.CallInterceptor(InterceptorType.OnError, context);

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

            await operationOptions.CallHook(HookType.OnUnsubscribed, context);
            await contractOptions.CallHook(HookType.OnUnsubscribed, context);
            await ClientOptions.CallInterceptor(InterceptorType.OnUnsubscribed, context);

        }

        public async Task<T> Ingest<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(IOperationRequest request, MethodInfo method, CancellationToken cancellationToken)
        {
            var context = new InvocationContext()
            {
                Services = ServiceProvider,
                CancellationToken = cancellationToken,
                Request = request,
                TryRefreshToken = client.TryRefreshToken
            };

            IContractOptions contractOptions = ClientOptions.GetContractOptions(method.ReflectedType!);
            IOperationOptions operationOptions = contractOptions!.GetOperationOptions(request.OperationName, method);

            bool remoteCancellation = operationOptions.RemoteCancellationIsAllowed ?? contractOptions.RemoteCancellationIsAllowed;

            await operationOptions.CallValidationHook(ServiceProvider, request, cancellationToken);

            try
            {
                await RateLimiterHelper.AcquireAsync(ClientOptions, ClientOptions.RateBucket, ClientOptions.IngestRateBucket, operationOptions.RateBucket);

                await operationOptions.CallHook(HookType.OnSend, context);
                await contractOptions.CallHook(HookType.OnSend, context);
                await ClientOptions.CallInterceptor(InterceptorType.OnSend, context);

                if (ClientOptions.UseHttpEndpointOverloading)
                    request.SetOperationName(method.GetMethodSignature(true));

                var response = await client.IngestMultiple<T>(request, remoteCancellation, ClientOptions, operationOptions, cancellationToken);

                await operationOptions.CallHook(HookType.OnAfterSend, context);
                await contractOptions.CallHook(HookType.OnAfterSend, context);
                await ClientOptions.CallInterceptor(InterceptorType.OnAfterSend, context);

                context.IsSuccess = response.Success;
                context.Result = response.Data;
                context.Error = response.Error;

                await operationOptions.CallHook(HookType.OnResponse, context);
                await contractOptions.CallHook(HookType.OnResponse, context);
                await ClientOptions.CallInterceptor(InterceptorType.OnResponse, context);

                return response.Data;
            }
            catch (Exception ex)
            {
                context.IsSuccess = false;
                context.Exception = ex;

                await operationOptions.CallHook(HookType.OnError, context);
                await contractOptions.CallHook(HookType.OnError, context);
                await ClientOptions.CallInterceptor(InterceptorType.OnError, context);

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
            var context = new InvocationContext()
            {
                Services = ServiceProvider,
                CancellationToken = cancellationToken,
                Request = request,
                TryRefreshToken = client.TryRefreshToken
            };

            IContractOptions contractOptions = ClientOptions.GetContractOptions(method.ReflectedType!);
            IOperationOptions operationOptions = contractOptions!.GetOperationOptions(request.OperationName, method);

            bool remoteCancellation = operationOptions.RemoteCancellationIsAllowed ?? contractOptions.RemoteCancellationIsAllowed;

            await operationOptions.CallValidationHook(ServiceProvider, request, cancellationToken);

            try
            {
                return HandleSubscription(request, remoteCancellation, method, contractOptions, operationOptions, context, cancellationToken);
            }
            catch (Exception ex)
            {
                context.IsSuccess = false;
                context.Exception = ex;

                await operationOptions.CallHook(HookType.OnError, context);
                await contractOptions.CallHook(HookType.OnError, context);
                await ClientOptions.CallInterceptor(InterceptorType.OnError, context);

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
            IContractOptions contractOptions,
            IOperationOptions operationOptions,
            InvocationContext context,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await RateLimiterHelper.AcquireAsync(ClientOptions, ClientOptions.RateBucket, ClientOptions.SubscriptionRateBucket, operationOptions.RateBucket);

            IObservable<JsonElement> observable;

            try
            {
                await operationOptions.CallHook(HookType.OnSend, context);
                await contractOptions.CallHook(HookType.OnSend, context);
                await ClientOptions.CallInterceptor(InterceptorType.OnSend, context);

                observable = await client.Subscribe<JsonElement>(request, remoteCancellation);

                await operationOptions.CallHook(HookType.OnAfterSend, context);
                await contractOptions.CallHook(HookType.OnAfterSend, context);
                await ClientOptions.CallInterceptor(InterceptorType.OnAfterSend, context);
            }
            catch (Exception ex)
            {
                context.IsSuccess = false;
                context.Exception = ex;

                await operationOptions.CallHook(HookType.OnError, context);
                await contractOptions.CallHook(HookType.OnError, context);
                await ClientOptions.CallInterceptor(InterceptorType.OnError, context);

                throw new HubconGenericException($"Error al obtener el stream del servidor. Mensaje: {ex.Message}", ex);
            }

            var options = new BoundedChannelOptions(5000);

            var observer = AsyncObserver.Create<JsonElement>(converter, options);

            try
            {
                using (observable.Subscribe(observer))
                {
                    await operationOptions.CallHook(HookType.OnSubscribed, context);
                    await contractOptions.CallHook(HookType.OnSubscribed, context);
                    await ClientOptions.CallInterceptor(InterceptorType.OnSubscribed, context);

                    var enumerator = observer.GetAsyncEnumerable(cancellationToken).GetAsyncEnumerator();

                    while (true)
                    {
                        JsonElement result = default;

                        try
                        {
                            if (!await enumerator.MoveNextAsync())
                                break;

                            await RateLimiterHelper.AcquireAsync(ClientOptions, ClientOptions.RateBucket, ClientOptions.SubscriptionRateBucket, operationOptions.RateBucket);

                            result = enumerator.Current;

                            await operationOptions.CallHook(HookType.OnEventReceived, context);
                            await contractOptions.CallHook(HookType.OnEventReceived, context);
                            await ClientOptions.CallInterceptor(InterceptorType.OnEventReceived, context);
                        }
                        catch (Exception ex)
                        {
                            context.IsSuccess = false;
                            context.Exception = ex;

                            await operationOptions.CallHook(HookType.OnError, context);
                            await contractOptions.CallHook(HookType.OnError, context);
                            await ClientOptions.CallInterceptor(InterceptorType.OnError, context);

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

                await operationOptions.CallHook(HookType.OnUnsubscribed, context);
                await contractOptions.CallHook(HookType.OnUnsubscribed, context);
                await ClientOptions.CallInterceptor(InterceptorType.OnUnsubscribed, context);
            }
        }

        // Devuelve true si el parámetro debería ir al body, false si va a query
        private static bool ShouldBindFromBody(Type type)
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

            string baseRestHttpUrl = string.Empty;
            string baseRestWebsocketUrl = string.Empty;

            if (baseUri!.IsAbsoluteUri)
            {
                baseRestHttpUrl = $"{baseUri!.Host}:{baseUri.Port}/{httpEndpoint ?? ""}".TrimEnd('/');
                baseRestWebsocketUrl = $"{baseUri!.Host}:{baseUri.Port}/{websocketEndpoint ?? "ws"}".TrimEnd('/');
            }
            else
            {
                baseRestHttpUrl = $"{baseUri!.OriginalString.TrimEnd('/')}/{httpEndpoint ?? ""}".TrimEnd('/');
                baseRestWebsocketUrl = $"{baseUri!.OriginalString.TrimEnd('/')}/{websocketEndpoint ?? "ws"}".TrimEnd('/');
            }

            _restHttpUrl = useSecureConnection ? $"https://{baseRestHttpUrl}" : $"http://{baseRestHttpUrl}";
            _websocketUrl = useSecureConnection ? $"wss://{baseRestWebsocketUrl}" : $"ws://{baseRestWebsocketUrl}";

            if (authenticationManagerType != null && options.AuthenticationManagerFactory != null)
            {
                authenticationManagerFactory = () => options.AuthenticationManagerFactory.GetValue<IAuthenticationManager>(serviceProvider);
            }

            client = new HubconWebSocketClient(new Uri(_websocketUrl), converter, options, serviceProvider, serviceProvider.GetService<ILogger<HubconWebSocketClient>>());

            client.AuthorizationTokenProvider = () => AuthenticationManager.AccessToken;

            client.WebSocketOptions = options.WebSocketOptions;

            client.LoggingEnabled = options.LoggingEnabled;

            client.ClientOptions = options;

            this.ServiceProvider = serviceProvider;

            ClientOptions = options;

            IsBuilt = true;
        }
    }
}
