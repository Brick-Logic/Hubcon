using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Client.Core.Exceptions;
using Hubcon.Client.Core.Helpers;
using Hubcon.Client.Core.HubconInvocationContext;
using Hubcon.Client.Core.Websockets;
using Hubcon.Shared.Abstractions.Attributes;
using Hubcon.Shared.Abstractions.Enums;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Abstractions.Standard.Extensions;
using Hubcon.Shared.Abstractions.Standard.Models;
using Hubcon.Shared.Core.Extensions;

using Hubcon.Shared.Core.Websockets.Events;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace Hubcon.Client.Integration.Client
{
    internal sealed class HubconClient : IHubconClient
    {
        private string _restHttpUrl = "";
        private string _websocketUrl = "";

        Func<IAuthenticationManager?>? authenticationManagerFactory;

        IAuthenticationManager? _authenticationManager;
        public IAuthenticationManager AuthenticationManager
        {
            get
            {
                if(_authenticationManager != null)
                    return _authenticationManager;

                var manager = authenticationManagerFactory?.Invoke() ?? throw new InvalidOperationException($"Authentication Manager not defined for server module '{ClientOptions.ServerModuleName}'.");

                _authenticationManager = manager;

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

        //private ConcurrentDictionary<MethodInfo, bool> NeedsAuth = new ConcurrentDictionary<MethodInfo, bool>();

        //private ConcurrentDictionary<MethodInfo, HttpMethod> MethodVerb = new ConcurrentDictionary<MethodInfo, HttpMethod>();

        //private ConcurrentDictionary<MethodInfo, string> MethodRoute = new ConcurrentDictionary<MethodInfo, string>();

        //private ConcurrentDictionary<MethodInfo, string?> BodyParameterName = new ConcurrentDictionary<MethodInfo, string?>();

        //private ConcurrentDictionary<MethodInfo, string?> QueryParameterName = new ConcurrentDictionary<MethodInfo, string?>();

        private readonly IDynamicConverter converter;
        private readonly IHttpClientFactory clientFactory;

        private readonly ConcurrentDictionary<MethodInfo, MethodCachedMetadata> _cachedMetadata = new();

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        public sealed class MethodCachedMetadata
        {
            // --- SECCIÓN DE REFERENCIAS (8 bytes c/u) ---
            // Total: 24 bytes
            public ReferenceHolder<string> MethodRouteHolder = new ReferenceHolder<string>();
            public ReferenceHolder<string> BodyParameterNameHolder = new ReferenceHolder<string>();
            public ReferenceHolder<string> QueryParameterNameHolder = new ReferenceHolder<string>();

            // --- SECCIÓN DE VALORES (4-8 bytes c/u con padding) ---
            // Total aproximado: 12-16 bytes
            public ReferenceHolder<HttpMethod> MethodVerbHolder = new ReferenceHolder<HttpMethod>();

            // El bool es el más pequeño, lo dejamos al final para rellenar huecos
            public PrimitiveHolder<bool> NeedsAuthHolder = new PrimitiveHolder<bool>();

            // Total estimado del objeto: ~48-56 bytes + overhead de objeto.
            // ENTRA PERFECTO en una línea de caché de 64 bytes.
        }

        // Lo usamos para bool, int, etc.
        public struct PrimitiveHolder<T> where T : struct
        {
            private T _value;
            private bool _hasValue;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public T GetOrAdd<TState>(TState state, Func<TState, T> factory)
            {
                // Fast path: El CPU predice este salto casi al 100% de éxito
                if (_hasValue) return _value;

                return Init(state, factory);
            }

            [MethodImpl(MethodImplOptions.NoInlining)] // Sacamos la lógica lenta del hot path
            private T Init<TState>(TState state, Func<TState, T> factory)
            {
                _value = factory(state);
                _hasValue = true;
                return _value;
            }
        }

        public struct ReferenceHolder<T> where T : class
        {
            private T? _value;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public T GetOrAdd<TState>(TState state, Func<TState, T> factory) => _value ??= factory(state);
        }

        public HubconClient(IDynamicConverter converter, IHttpClientFactory clientFactory)
        {
            this.converter = converter;
            this.clientFactory = clientFactory;
        }

        public async Task<T> SendAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(IOperationRequest request, MethodInfo methodInfo, CancellationToken cancellationToken)
        {
            var metadata = _cachedMetadata.GetOrAdd(methodInfo, static _ => new MethodCachedMetadata());

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

                    //if (ClientOptions.UseHttpEndpointOverloading)
                    //    request.SetOperationName(methodInfo.GetMethodSignature(true));

                    var result = await client.InvokeAsync<T>(request, remoteCancellation, cancellationToken);

                    HubconContext.Current.Response = result;

                    await operationOptions.CallHook(HookType.OnAfterSend, context);
                    await contractOptions.CallHook(HookType.OnAfterSend, context);
                    await ClientOptions.CallInterceptor(InterceptorType.OnAfterSend, context);

                    await operationOptions.CallHook(HookType.OnResponse, context);
                    await contractOptions.CallHook(HookType.OnResponse, context);
                    await ClientOptions.CallInterceptor(InterceptorType.OnResponse, context);

                    return result.Data;
                }
                else if (ClientOptions.IsNonHubconServer)
                {
                    await RateLimiterHelper.AcquireAsync(ClientOptions, ClientOptions.RateBucket, ClientOptions.HttpFireAndForgetRateBucket, operationOptions.RateBucket);

                    HttpMethod httpMethod = GetHttpMethod(request, metadata, methodInfo);
                    string finalRoute = GetFinalRoute(methodInfo, metadata);

                    Dictionary<string, object> remainingArguments = GetRemainingArguments(request, ref finalRoute);

                    StringContent? content = null;
                    string url;

                    url = BuildBodyAndFinalUrl(request, metadata, methodInfo, httpMethod, finalRoute, remainingArguments, ref content);

                    var httpRequest = new HttpRequestMessage(httpMethod, url);

                    if (content != null)
                        httpRequest.Content = content;

                    bool needsAuth = metadata.NeedsAuthHolder.GetOrAdd(methodInfo, _ =>
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
                        return default!;

                    await operationOptions.CallHook(HookType.OnResponse, context);
                    await contractOptions.CallHook(HookType.OnResponse, context);
                    await ClientOptions.CallInterceptor(InterceptorType.OnResponse, context);
                   
                    var res = converter.DeserializeJsonElement<HubconResponse<T>>(result) ?? default!;

                    HubconContext.Current.Response = res;

                    content?.Dispose();
                    httpRequest.Dispose();
                    response.Dispose();

                    return res.Data!;
                }
                else
                {
                    await RateLimiterHelper.AcquireAsync(ClientOptions, ClientOptions.RateBucket, ClientOptions.HttpFireAndForgetRateBucket, operationOptions.RateBucket);

                    HttpMethod httpMethod = metadata.MethodVerbHolder.GetOrAdd(methodInfo, method =>
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

                    bool needsAuth = metadata.NeedsAuthHolder.GetOrAdd(methodInfo, _ =>
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

                    var operationResponse = converter.DeserializeJsonElement<HubconResponse<T>>(result)
                        ?? throw new HubconGenericException("No se recibió ningun mensaje del servidor.");

                    HubconContext.Current.Response = operationResponse;

                    context.IsSuccess = operationResponse.Success;
                    context.Result = operationResponse.Data;
                    context.Error = operationResponse.Error;
                    context.StatusCode = operationResponse.StatusCode;

                    await operationOptions.CallHook(HookType.OnResponse, context);
                    await contractOptions.CallHook(HookType.OnResponse, context);
                    await ClientOptions.CallInterceptor(InterceptorType.OnResponse, context);

                    content?.Dispose();
                    httpRequest.Dispose();
                    response.Dispose();
                    return operationResponse.Data!;
                }
            }
            catch (Exception ex)
            {
                context.IsSuccess = false;
                context.Exception = ex;

                await operationOptions.CallHook(HookType.OnError, context);
                await contractOptions.CallHook(HookType.OnError, context);
                await ClientOptions.CallInterceptor(InterceptorType.OnError, context);

                if (HubconContext.Current.IsWrapped)
                {
                    HubconContext.Current.Exception = ex;
                    HubconContext.Current.Response = HubconResponse.InternalError<T>(ex);
                    return default!;
                }
                
                throw;
            }
        }

        public async Task CallAsync(IOperationRequest request, MethodInfo methodInfo, CancellationToken cancellationToken)
        {
            if (!IsBuilt)
                throw new InvalidOperationException("El cliente no ha sido construido. Asegúrese de llamar a 'Build()' antes de usar este método.");

            var metadata = _cachedMetadata.GetOrAdd(methodInfo, static _ => new MethodCachedMetadata());

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
                else if (ClientOptions.IsNonHubconServer)
                {
                    await RateLimiterHelper.AcquireAsync(ClientOptions, ClientOptions.RateBucket, ClientOptions.HttpFireAndForgetRateBucket, operationOptions.RateBucket);

                    HttpMethod httpMethod = GetHttpMethod(request, metadata, methodInfo);
                    string finalRoute = GetFinalRoute(methodInfo, metadata);
                    Dictionary<string, object> remainingArguments = GetRemainingArguments(request, ref finalRoute);

                    StringContent? content = null;
                    string url;

                    url = BuildBodyAndFinalUrl(request, metadata, methodInfo, httpMethod, finalRoute, remainingArguments, ref content);

                    var httpRequest = new HttpRequestMessage(httpMethod, url);

                    if (content != null)
                        httpRequest.Content = content;

                    bool needsAuth = metadata.NeedsAuthHolder.GetOrAdd(methodInfo, _ =>
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

                    var response = await HttpClient.SendAsync(httpRequest, cancellationToken);

                    await operationOptions.CallHook(HookType.OnAfterSend, context);
                    await contractOptions.CallHook(HookType.OnAfterSend, context);
                    await ClientOptions.CallInterceptor(InterceptorType.OnAfterSend, context);

                    content?.Dispose();
                    httpRequest.Dispose();
                    response.Dispose();
                }
                else
                {
                    await RateLimiterHelper.AcquireAsync(ClientOptions, ClientOptions.RateBucket, ClientOptions.HttpFireAndForgetRateBucket, operationOptions.RateBucket);

                    HttpMethod httpMethod = metadata.MethodVerbHolder.GetOrAdd(methodInfo, method =>
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

                    bool needsAuth = metadata.NeedsAuthHolder.GetOrAdd(methodInfo, _ =>
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

                    var response = await HttpClient.SendAsync(httpRequest, cancellationToken);

                    await operationOptions.CallHook(HookType.OnAfterSend, context);
                    await contractOptions.CallHook(HookType.OnAfterSend, context);
                    await ClientOptions.CallInterceptor(InterceptorType.OnAfterSend, context);

                    content?.Dispose();
                    httpRequest.Dispose();
                    response.Dispose();
                }
            }
            catch (Exception ex)
            {
                context.Exception = ex;
                context.IsSuccess = false;

                await operationOptions.CallHook(HookType.OnError, context);
                await contractOptions.CallHook(HookType.OnError, context);
                await ClientOptions.CallInterceptor(InterceptorType.OnError, context);

                if (HubconContext.Current.IsWrapped)
                {
                    HubconContext.Current.Exception = ex;
                    HubconContext.Current.Response = HubconResponse.InternalError(ex);
                    return;
                }

                throw;
            }
        }

        private string BuildBodyAndFinalUrl(IOperationRequest request, MethodCachedMetadata metadata, MethodInfo methodInfo, HttpMethod httpMethod, string finalRoute, Dictionary<string, object> remainingArguments, ref StringContent? content)
        {
            string url;
            // 3. Construcción de Body o QueryString según el Verbo
            if (httpMethod == HttpMethod.Post || httpMethod == HttpMethod.Put)
            {
                object? bodyData = null;

                // Intentamos obtener el nombre del parámetro marcado con [Body]
                var bodyParamName = metadata.BodyParameterNameHolder.GetOrAdd(methodInfo, method =>
                    method.GetParameters()
                          .FirstOrDefault(p => p.GetCustomAttribute<AsBodyAttribute>() != null)?.Name);

                // Si existe un parámetro [Body] y está en los argumentos, lo extraemos (Aplanamiento)
                if (bodyParamName != null && request.Arguments.TryGetValue(bodyParamName, out var explicitBody))
                {
                    bodyData = explicitBody;
                }
                else
                {
                    // Lógica original: Solo enviamos en el Body lo que NO se usó en la URL
                    // Si queda solo un argumento llamado "value", lo desempaquetamos.
                    // Si no, enviamos el diccionario con lo restante.
                    bodyData = remainingArguments.Count == 1 && remainingArguments.ContainsKey("value")
                                ? remainingArguments["value"]
                                : remainingArguments;
                }

                var jsonBody = converter.Serialize(bodyData);
                content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                url = _restHttpUrl.TrimEnd('/') + "/" + finalRoute.TrimStart('/');
            }
            else // GET o DELETE
            {
                var builder = new UriBuilder(_restHttpUrl);
                builder.Path = (builder.Path.TrimEnd('/') + "/" + finalRoute.TrimStart('/')).Replace("//", "/");
                var query = System.Web.HttpUtility.ParseQueryString(builder.Query);

                // Intentamos obtener el nombre del parámetro marcado con [AsQuery]
                var queryParamName = metadata.QueryParameterNameHolder.GetOrAdd(methodInfo, method =>
                    method.GetParameters()
                          .FirstOrDefault(p => p.GetCustomAttribute<AsQueryAttribute>() != null)?.Name);

                // Si hay un objeto [AsQuery], lo aplanamos
                if (queryParamName != null && remainingArguments.TryGetValue(queryParamName, out var queryObj) && queryObj != null)
                {
                    // Usamos reflexión (o podrías usar el converter si tiene un ToDictionary) 
                    // para extraer las propiedades del objeto a la QueryString
                    var props = queryObj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
                    foreach (var prop in props)
                    {
                        var val = prop.GetValue(queryObj);
                        if (val != null) query[prop.Name] = val.ToString();
                    }

                    // Removemos el objeto original de los restantes para que no se duplique
                    remainingArguments.Remove(queryParamName);
                }

                // El resto de argumentos sobrantes van como parámetros normales
                foreach (var arg in remainingArguments)
                {
                    query[arg.Key] = arg.Value?.ToString() ?? "";
                }

                builder.Query = query.ToString();
                url = builder.ToString();
            }

            return url;
        }

        private static Dictionary<string, object> GetRemainingArguments(IOperationRequest request, ref string finalRoute)
        {

            // 2. Lógica de Reemplazo en URL (Path Parameters)
            // Copiamos los argumentos a una lista de trabajo para saber cuáles sobran (y van al Body o Query)
            var remainingArguments = request.Arguments.ToDictionary(k => k.Key, v => v.Value);

            foreach (var arg in request.Arguments)
            {
                string placeholder = $"{{{arg.Key}}}";
                if (finalRoute.Contains(placeholder))
                {
                    finalRoute = finalRoute.Replace(placeholder, Uri.EscapeDataString(arg.Value?.ToString() ?? ""));
                    remainingArguments.Remove(arg.Key); // Ya se usó en el Path, lo quitamos
                }
            }

            return remainingArguments;
        }

        private string GetFinalRoute(MethodInfo methodInfo, MethodCachedMetadata metadata)
        {
            return metadata.MethodRouteHolder.GetOrAdd(methodInfo, method =>
            {
                if (method.GetCustomAttribute<HttpGetAttribute>() != null) return method.GetCustomAttribute<HttpGetAttribute>().Template;
                if (method.GetCustomAttribute<HttpPostAttribute>() != null) return method.GetCustomAttribute<HttpPostAttribute>().Template;
                if (method.GetCustomAttribute<HttpPutAttribute>() != null) return method.GetCustomAttribute<HttpPutAttribute>().Template;
                if (method.GetCustomAttribute<HttpDeleteAttribute>() != null) return method.GetCustomAttribute<HttpDeleteAttribute>().Template;

                return "/";
            });
        }

        private HttpMethod GetHttpMethod(IOperationRequest request, MethodCachedMetadata metadata, MethodInfo methodInfo)
        {
            // 1. Mapeo de Atributos a Verbos HTTP
            HttpMethod httpMethod = metadata.MethodVerbHolder.GetOrAdd(methodInfo, method =>
            {
                if (method.GetCustomAttribute<HttpGetAttribute>() != null) return HttpMethod.Get;
                if (method.GetCustomAttribute<HttpPostAttribute>() != null) return HttpMethod.Post;
                if (method.GetCustomAttribute<HttpPutAttribute>() != null) return HttpMethod.Put;
                if (method.GetCustomAttribute<HttpDeleteAttribute>() != null) return HttpMethod.Delete;

                // Fallback basado en argumentos como tenías antes
                return request.Arguments.Count > 0 ? HttpMethod.Post : HttpMethod.Get;
            });
            return httpMethod;
        }

        public async IAsyncEnumerable<JsonElement> GetStream(IOperationRequest request, MethodInfo methodInfo, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var metadata = _cachedMetadata.GetOrAdd(methodInfo, static _ => new MethodCachedMetadata());

            var context = new InvocationContext()
            {
                Services = ServiceProvider,
                CancellationToken = cancellationToken,
                Request = request,
                TryRefreshToken = client.TryRefreshToken
            };

            IAsyncEnumerable<JsonElement>? enumerable = null;

            IContractOptions contractOptions = ClientOptions.GetContractOptions(methodInfo.ReflectedType!);
            IOperationOptions operationOptions = contractOptions!.GetOperationOptions(request.OperationName, methodInfo);

            bool remoteCancellation = operationOptions.RemoteCancellationIsAllowed ?? contractOptions.RemoteCancellationIsAllowed;

            IObservable<JsonElement> observable;

            await operationOptions.CallValidationHook(ServiceProvider, request, cancellationToken);

            await RateLimiterHelper.AcquireAsync(ClientOptions, ClientOptions.RateBucket, ClientOptions.StreamingRateBucket, operationOptions.RateBucket);

            await operationOptions.CallHook(HookType.OnSend, context);
            await contractOptions.CallHook(HookType.OnSend, context);
            await ClientOptions.CallInterceptor(InterceptorType.OnSend, context);
            bool isWebsocketOperation = contractOptions.IsWebsocketOperation(request.OperationName);

            if (ClientOptions.IsNonHubconServer || !isWebsocketOperation)
            {
                await RateLimiterHelper.AcquireAsync(ClientOptions, ClientOptions.RateBucket, ClientOptions.HttpFireAndForgetRateBucket, operationOptions.RateBucket);

                HttpMethod httpMethod = GetHttpMethod(request, metadata, methodInfo);
                string finalRoute = GetFinalRoute(methodInfo, metadata);
                Dictionary<string, object> remainingArguments = GetRemainingArguments(request, ref finalRoute);

                StringContent? content = null;
                string url;

                if (!isWebsocketOperation)
                {
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
                }
                else
                {
                    url = BuildBodyAndFinalUrl(request, metadata, methodInfo, httpMethod, finalRoute, remainingArguments, ref content);
                }

                var httpRequest = new HttpRequestMessage(httpMethod, url);

                if (content != null)
                    httpRequest.Content = content;

                bool needsAuth = metadata.NeedsAuthHolder.GetOrAdd(methodInfo, _ =>
                {
                    return (operationOptions.HttpAuthIsEnabled ?? true)
                        && (contractOptions.HttpAuthIsEnabled)
                        && ClientOptions.HttpAuthIsEnabled;
                });

                if (needsAuth && AuthenticationManager.IsSessionActive)
                    httpRequest.Headers.Authorization = new AuthenticationHeaderValue(AuthenticationManager.TokenType!, AuthenticationManager.AccessToken);

                HttpResponseMessage response; 

                try
                {
                    response = await HttpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                    response.EnsureSuccessStatusCode();
                }
                catch (Exception ex)
                {
                    context.Exception = ex;
                    context.IsSuccess = false;

                    await operationOptions.CallHook(HookType.OnError, context);
                    await contractOptions.CallHook(HookType.OnError, context);
                    await ClientOptions.CallInterceptor(InterceptorType.OnError, context);

                    if (HubconContext.Current.IsWrapped)
                    {
                        HubconContext.Current.Exception = ex;
                        HubconContext.Current.Response = HubconResponse.InternalError<IAsyncEnumerable<JsonElement>>(ex);
                    }

                    throw;
                }

                await operationOptions.CallHook(HookType.OnAfterSend, context);
                await contractOptions.CallHook(HookType.OnAfterSend, context);
                await ClientOptions.CallInterceptor(InterceptorType.OnAfterSend, context);

                using var stream = await response.Content.ReadAsStreamAsync();

                enumerable = ParseSSEStream(stream, cancellationToken);

                await foreach (var item in enumerable.WithCancellation(cancellationToken))
                {
                    yield return item;
                }

                content?.Dispose();
                httpRequest.Dispose();
                response.Dispose();
            }
            else
            {
                try
                {             
                    observable = await client.Stream<JsonElement>(request, remoteCancellation, cancellationToken);

                    if (HubconContext.Current.IsWrapped == true)
                        HubconContext.Current.Response = HubconResponse.OkT<IAsyncEnumerable<JsonElement>>();
                }
                catch (Exception ex)
                {
                    context.Exception = ex;
                    context.IsSuccess = false;

                    await operationOptions.CallHook(HookType.OnError, context);
                    await contractOptions.CallHook(HookType.OnError, context);
                    await ClientOptions.CallInterceptor(InterceptorType.OnError, context);

                    if (HubconContext.Current.IsWrapped)
                    {
                        HubconContext.Current.Exception = ex;
                        HubconContext.Current.Response = HubconResponse.InternalError<IAsyncEnumerable<JsonElement>>(ex);
                    }

                    throw;
                }

                await operationOptions.CallHook(HookType.OnAfterSend, context);
                await contractOptions.CallHook(HookType.OnAfterSend, context);
                await ClientOptions.CallInterceptor(InterceptorType.OnAfterSend, context);

                var observer = AsyncObserver.Create<JsonElement>(converter);
                enumerable = observer.GetAsyncEnumerable(cancellationToken);

                using (observable.Subscribe(observer))
                {
                    var enumerator = enumerable.GetAsyncEnumerator(cancellationToken);

                    await operationOptions.CallHook(HookType.OnSubscribed, context);
                    await contractOptions.CallHook(HookType.OnSubscribed, context);
                    await ClientOptions.CallInterceptor(InterceptorType.OnSubscribed, context);

                    while (true)
                    {
                        JsonElement result = default;
                        try
                        {
                            if (!await enumerator.MoveNextAsync() || cancellationToken.IsCancellationRequested)
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

                            if (HubconContext.Current.IsWrapped)
                            {
                                HubconContext.Current.Exception = ex;
                                HubconContext.Current.Response = HubconResponse.InternalError<IAsyncEnumerable<JsonElement>>(ex);
                            }

                            throw;
                        }

                        yield return result;
                    }
                }
            }

            await operationOptions.CallHook(HookType.OnUnsubscribed, context);
            await contractOptions.CallHook(HookType.OnUnsubscribed, context);
            await ClientOptions.CallInterceptor(InterceptorType.OnUnsubscribed, context);
        }

        public async IAsyncEnumerable<JsonElement> ParseSSEStream(Stream stream, CancellationToken cancellationToken)
        {
            using var reader = new StreamReader(stream);
            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: ")) continue;

                string jsonData = line.Substring(6);
                if (jsonData == "[DONE]") break;

                var ev = converter.DeserializeData<JsonElement>(jsonData);

                if (ev.ValueKind != JsonValueKind.Null) yield return ev;
            }
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

                if (HubconContext.Current.IsWrapped == true)
                    HubconContext.Current.Response = response;

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

                if (HubconContext.Current.IsWrapped)
                {
                    HubconContext.Current.Exception = ex;
                    HubconContext.Current.Response = HubconResponse.InternalError<T>(ex);
                    return default!;
                }
                
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

                if (HubconContext.Current.IsWrapped)
                {
                    HubconContext.Current.Exception = ex;
                    HubconContext.Current.Response = HubconResponse.InternalError<IAsyncEnumerable<JsonElement>>(ex);
                }

                throw;
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

                if (HubconContext.Current.IsWrapped)
                {
                    HubconContext.Current.Exception = ex;
                    HubconContext.Current.Response = HubconResponse.InternalError<IAsyncEnumerable<JsonElement>>(ex);
                }

                throw;
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

                            if (HubconContext.Current.IsWrapped)
                            {
                                HubconContext.Current.Exception = ex;
                                HubconContext.Current.Response = HubconResponse.InternalError<IAsyncEnumerable<JsonElement>>(ex);
                            }

                            throw;
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
