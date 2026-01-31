using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Client.Core.Configurations;
using Hubcon.Client.Core.Helpers;
using Hubcon.Client.Core.HubconInvocationContext;
using Hubcon.Shared.Abstractions.Attributes;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Core.Extensions;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
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
    public class NonHubconHttpTransportClient : ITransportClient<NonHubconHttpTransport>
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
            var baseUrl = context.BaseUrl;
            var route = methodInfo.GetRoute(false);
            string finalRoute = httpMethod.Template;

            Dictionary<string, object> remainingArguments = HttpMessageHelper.GetRemainingArguments(request, context.Converter, ref finalRoute);

            url = HttpMessageHelper.BuildBodyAndFinalUrl(request, context, finalRoute, remainingArguments, ref content);
        
            var httpRequest = new HttpRequestMessage(httpMethod.HttpMethod, url);
            httpRequest.Content = content;

            if (context.RequiresAuthentication && authenticationManager != null && authenticationManager.IsSessionActive)
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue(authenticationManager.TokenType!, authenticationManager.AccessToken);

            var response = await httpClient.SendAsync(httpRequest, cancellationToken);

            HubconResponse methodReponse = new HubconResponse(
                response.IsSuccessStatusCode,
                !response.IsSuccessStatusCode,
                "",
                "",
                (int)response.StatusCode
            );

            context.CallContext.SetResponse(methodReponse);

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
            var httpMethod = context.HttpMethodAttribute!;
            var authenticationManager = context.AuthenticationManagerFactory?.Invoke();
            var httpClient = GetHttpClient(context);
            var converter = context.Converter;
            var route = methodInfo.GetRoute(false);
            string finalRoute = httpMethod.Template;

            Dictionary<string, object> remainingArguments = HttpMessageHelper.GetRemainingArguments(request, converter, ref finalRoute);
            url = HttpMessageHelper.BuildBodyAndFinalUrl(request, context, finalRoute, remainingArguments, ref content);

            var httpRequest = new HttpRequestMessage(httpMethod.HttpMethod, url);
            httpRequest.Content = content;


            if (context.RequiresAuthentication && authenticationManager != null && authenticationManager.IsSessionActive)
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue(authenticationManager.TokenType!, authenticationManager.AccessToken);

            HttpResponseMessage response = await httpClient.SendAsync(httpRequest, cancellationToken);

            var responseBytes = await response.Content.ReadAsByteArrayAsync();
            var result = converter.DeserializeByteArray<JsonElement>(responseBytes);

            var res = converter.DeserializeJsonElement<T>(result) ?? default!;

            IHubconResponse methodReponse = new HubconResponse(
                response.IsSuccessStatusCode,
                !response.IsSuccessStatusCode,
                "",
                "",
                (int)response.StatusCode,
                res
            );

            context.CallContext.SetResponse(methodReponse);

            content?.Dispose();
            httpRequest.Dispose();
            response.Dispose();

            return (T)methodReponse.Data!;
        }
    }
}
