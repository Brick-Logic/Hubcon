using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Client.Core.Configurations;
using Hubcon.Client.Core.Helpers;
using Hubcon.Shared.Abstractions.Attributes;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Core.Extensions;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hubcon;
using System.Runtime.CompilerServices;

namespace Hubcon.Client.Core.Transports
{
    public sealed class HttpTransportClient : ITransportClient<HttpTransport>
    {
        HttpClient? _httpClient;

        private HttpClient GetHttpClient(IClientOperationContext context)
        {
            if (_httpClient != null)
                return _httpClient;

            _httpClient ??= context.ClientOptions.HttpClientFactory.Invoke(context.ServiceProvider);
            context.ClientOptions.HttpClientOptions?.Invoke(_httpClient, context.ServiceProvider);

            return _httpClient;
        }

        public async Task<HubconResponse<bool>> CallAsync(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken = default)
        {
            StringContent? content = null;
            var url = "";
            var methodInfo = (context.Member as MethodInfo)!;
            var httpMethod = context.HttpMethodAttribute!;
            var authenticationManager = context.AuthenticationManagerFactory?.Invoke();
            var httpClient = GetHttpClient(context);
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

            url += methodInfo.GetRoute(false).FullRoute;
            var httpRequest = new HttpRequestMessage(httpMethod.HttpMethod, url);

            if (content != null)
                httpRequest.Content = content;

            if (context.RequiresAuthentication && authenticationManager != null && authenticationManager.IsSessionActive)
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue(authenticationManager.TokenType!, authenticationManager.AccessToken);

            var response = await httpClient.SendAsync(httpRequest, cancellationToken);

            content?.Dispose();
            httpRequest.Dispose();
            response.Dispose();

            return true;
        }

        public async IAsyncEnumerable<JsonElement> GetStream(IOperationRequest request, IClientOperationContext context, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            StringContent? content = null;
            var url = context.HttpUrl;
            var methodInfo = (context.Member as MethodInfo)!;
            var httpMethod = context.HttpMethodAttribute!;
            var authenticationManager = context.AuthenticationManagerFactory?.Invoke();
            var httpClient = GetHttpClient(context);
            string finalRoute = httpMethod.Template;

            Dictionary<string, object> remainingArguments = HttpMessageHelper.GetRemainingArguments(request, context.Converter, ref finalRoute);
            url = HttpMessageHelper.BuildBodyAndFinalUrl(request, context, finalRoute, remainingArguments, ref content);


            var httpRequest = new HttpRequestMessage(httpMethod.HttpMethod, url);
            httpRequest.SetBrowserResponseStreamingEnabled(true);
            httpRequest.Content = content;

            if (context.RequiresAuthentication && authenticationManager != null && authenticationManager.IsSessionActive)
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue(authenticationManager.TokenType!, authenticationManager.AccessToken);

            HttpResponseMessage response;
            response = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            using var stream = await response.Content.ReadAsStreamAsync();

            var enumerable = HttpMessageHelper.ParseSSEStream(stream, context, cancellationToken);

            await foreach (var item in enumerable.WithCancellation(cancellationToken))
            {
                yield return item;
            }

            content?.Dispose();
            httpRequest.Dispose();
            response.Dispose();
        }

        public IAsyncEnumerable<JsonElement> GetSubscription(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<HubconResponse<T>> Ingest<T>(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public async Task<HubconResponse<T>> SendAsync<T>(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken = default)
        {
            StringContent? content = null;
            var url = "";
            var methodInfo = (context.Member as MethodInfo)!;
            var httpMethod = context.HttpMethodDefined!;
            var authenticationManager = context.AuthenticationManagerFactory?.Invoke();
            var httpClient = GetHttpClient(context);
            var converter = context.Converter;

            if (context.HttpMethodDefined == HttpMethod.Post || context.HttpMethodDefined == HttpMethod.Put)
            {
                HandlePost(request, context, out content, context.HttpUrl, out url, methodInfo);
            }
            else
            {
                url = HandleGeneric(request, context, context.HttpUrl, methodInfo);
            }

            var httpRequest = new HttpRequestMessage(httpMethod, url);

            if(content != null)
                httpRequest.Content = content;

            if (context.RequiresAuthentication && authenticationManager != null && authenticationManager.IsSessionActive)
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue(authenticationManager.TokenType!, authenticationManager.AccessToken);

            HttpResponseMessage response = await httpClient.SendAsync(httpRequest, cancellationToken);

            var responseBytes = await response.Content.ReadAsByteArrayAsync();
            var result = converter.DeserializeByteArray<JsonElement>(responseBytes);

            HubconResponse<T> operationResponse = converter.DeserializeJsonElement<HubconResponse<T>>(result);

            content?.Dispose();
            httpRequest.Dispose();
            response.Dispose();
            return operationResponse!;
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
    }
}
