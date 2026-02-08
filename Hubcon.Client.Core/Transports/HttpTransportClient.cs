using Hubcon.Client.Core.Helpers;
using Hubcon.Shared.Core.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Hubcon.Client.Core.Transports
{
    public sealed class HttpTransportClient : TransportClient<HttpTransport>
    {
        HttpClient _httpClient = null!;

        public override async ValueTask CallAsync(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken = default)
        {
            StringContent? content = null;
            HttpRequestMessage? httpRequest = null;
            HttpResponseMessage? response = null;
            try
            {
                var url = "";
                var methodInfo = (context.Member as MethodInfo)!;
                var httpMethod = context.HttpMethodAttribute!;
                var authenticationManager = context.AuthenticationManagerFactory?.Invoke();
                var converter = context.Converter;
                var route = methodInfo.GetRoute(false);

                if (httpMethod is HttpPostAttribute)
                {
                    var arguments = converter.Serialize(request.Arguments);
                    content = new StringContent(arguments, Encoding.UTF8, "application/json");
                    url = context.HttpUrl + route.FullRoute;
                }
                else
                {
                    var builder = new UriBuilder(context.HttpUrl);

                    var query = System.Web.HttpUtility.ParseQueryString(builder.Query);

                    foreach (var argument in request.Arguments)
                    {
                        query[argument.Key] = argument.Value?.ToString() ?? "";
                    }

                    builder.Path = route.FullRoute;
                    builder.Query = query.ToString();
                    url = builder.ToString();
                }

                httpRequest = new HttpRequestMessage(httpMethod.HttpMethod, url);

                foreach (var header in await context.GetHeaders(context.ScopeServiceProvider))
                    httpRequest.Headers.Add(header.Key, header.Value);

                if (content != null)
                    httpRequest.Content = content;

                if (context.RequiresAuthentication && authenticationManager != null && authenticationManager.IsSessionActive)
                    httpRequest.Headers.Authorization = new AuthenticationHeaderValue(authenticationManager.TokenType!, authenticationManager.AccessToken);

                response = await _httpClient.SendAsync(httpRequest, cancellationToken);

                HubconResponse methodReponse = new HubconResponse(
                    response.IsSuccessStatusCode,
                    !response.IsSuccessStatusCode,
                    "",
                    "",
                    (int)response.StatusCode,
                    null,
                    response.Content.ToString()
                );

                await context.SetResponse(methodReponse);
            }
            catch (Exception ex)
            {
                await context.SetResponse(HubconResponse.InternalError(ex, originalData: response?.Content?.ToString()));
            }
            finally
            {
                content?.Dispose();
                httpRequest?.Dispose();
                response?.Dispose();
            }
        }

        public override async ValueTask<IAsyncEnumerable<JsonElement>> GetStream(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken = default)
        {          
            StringContent? content = null;
            HttpRequestMessage? httpRequest = null;
            HttpResponseMessage? response = null;
            try
            {
                var url = "";
                var methodInfo = (context.Member as MethodInfo)!;
                var httpMethod = context.HttpMethodAttribute!;
                var authenticationManager = context.AuthenticationManagerFactory?.Invoke();
                var converter = context.Converter;
                var route = methodInfo.GetRoute(false);

                if (httpMethod is HttpPostAttribute)
                {
                    var arguments = converter.Serialize(request.Arguments);
                    content = new StringContent(arguments, Encoding.UTF8, "application/json");
                    url = context.HttpUrl + route.FullRoute;
                }
                else
                {
                    var builder = new UriBuilder(context.HttpUrl);

                    var query = System.Web.HttpUtility.ParseQueryString(builder.Query);

                    foreach (var argument in request.Arguments)
                    {
                        query[argument.Key] = argument.Value?.ToString() ?? "";
                    }

                    builder.Path = route.FullRoute;
                    builder.Query = query.ToString();
                    url = builder.ToString();
                }

                httpRequest = new HttpRequestMessage(httpMethod.HttpMethod, url);
                httpRequest.SetBrowserResponseStreamingEnabled(true);

                foreach (var header in await context.GetHeaders(context.ScopeServiceProvider))
                    httpRequest.Headers.Add(header.Key, header.Value);

                if (content != null)
                    httpRequest.Content = content;

                if (context.RequiresAuthentication && authenticationManager != null && authenticationManager.IsSessionActive)
                    httpRequest.Headers.Authorization = new AuthenticationHeaderValue(authenticationManager.TokenType!, authenticationManager.AccessToken);

                response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                var enumerable = HttpMessageHelper.ParseSSEStream(response, context, cancellationToken);

                var methodReponse = new HubconResponse<IAsyncEnumerable<JsonElement>>(
                    response.IsSuccessStatusCode,
                    !response.IsSuccessStatusCode,
                    "",
                    "",
                    (int)response.StatusCode,
                    enumerable,
                    response.Content.ToString()
                );

                await context.SetResponse(methodReponse);
                return enumerable;
            }
            catch (Exception ex)
            {
                await context.SetResponse(HubconResponse.InternalError(ex, originalData: response?.Content?.ToString()));
                return default!;
            }
            finally
            {
                content?.Dispose();
                httpRequest?.Dispose();
            }
        }

        public override ValueTask Ingest<T>(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public override async ValueTask SendAsync<T>(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken = default)
        {         
            StringContent? content = null;
            HttpRequestMessage? httpRequest = null;
            HttpResponseMessage? response = null;
            try
            {
                var url = "";
                var methodInfo = (context.Member as MethodInfo)!;
                var httpMethod = context.HttpMethodDefined!;
                var authenticationManager = context.AuthenticationManagerFactory?.Invoke();
                var converter = context.Converter;

                if (context.HttpMethodDefined == HttpMethod.Post || context.HttpMethodDefined == HttpMethod.Put)
                {
                    HandlePost(request, context, out content, context.HttpUrl, out url, methodInfo);
                }
                else
                {
                    url = HandleGeneric(request, context, context.HttpUrl, methodInfo);
                }

                httpRequest = new HttpRequestMessage(httpMethod, url);

                foreach (var header in await context.GetHeaders(context.ScopeServiceProvider))
                    httpRequest.Headers.Add(header.Key, header.Value);

                if (content != null)
                    httpRequest.Content = content;

                if (context.RequiresAuthentication && authenticationManager != null && authenticationManager.IsSessionActive)
                    httpRequest.Headers.Authorization = new AuthenticationHeaderValue(authenticationManager.TokenType!, authenticationManager.AccessToken);

                response = await _httpClient.SendAsync(httpRequest, cancellationToken);

                var responseBytes = await response.Content.ReadAsByteArrayAsync();
                var result = converter.DeserializeByteArray<JsonElement>(responseBytes);

                await context.HandleResponse<T>(result);
            }
            catch (Exception ex)
            {
                await context.SetResponse(HubconResponse.InternalError<T>(ex, originalData: response?.Content?.ToString()));
            }
            finally
            {
                content?.Dispose();
                httpRequest?.Dispose();
                response?.Dispose();
            }
        }

        private static string HandleGeneric(IOperationRequest request, IClientOperationContext context, string currentUrl, MethodInfo methodInfo)
        {
            var builder = new UriBuilder(currentUrl);

            var query = System.Web.HttpUtility.ParseQueryString(builder.Query);

            foreach (var argument in request.Arguments)
            {
                query[argument.Key] = argument.Value?.ToString() ?? "";
            }

            builder.Path = methodInfo.GetRoute(context.ClientOptions.UseHttpEndpointOverloading).FullRoute;
            builder.Query = query.ToString();
            currentUrl = builder.ToString();
            return currentUrl;
        }

        private static void HandlePost(IOperationRequest request, IClientOperationContext context, out StringContent? content, string currentUrl, out string url, MethodInfo methodInfo)
        {
            var arguments = context.Converter.Serialize(request.Arguments);
            content = new StringContent(arguments, Encoding.UTF8, "application/json");
            url = currentUrl + methodInfo.GetRoute(context.ClientOptions.UseHttpEndpointOverloading).FullRoute;
        }

        protected override void Build(TransportContext configuration)
        {
            _httpClient = configuration.ClientOptions.HttpClientFactory.Invoke(configuration.ProxyServiceProvider);
            configuration.ClientOptions.HttpClientOptions?.Invoke(_httpClient, configuration.ProxyServiceProvider);;
        }
    }
}
