using Hubcon.Client.Core.Helpers;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Core.Extensions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Hubcon.Client.Core.Transports
{
    public sealed class NonHubconHttpTransportClient : TransportClient<NonHubconHttpTransport>
    {
        HttpClient _httpClient = null!;
        
        public override async ValueTask CallAsync(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken = default)
        {
            StringContent? content = null;
            var url = "";
            var methodInfo = (context.Member as MethodInfo)!;
            var httpMethod = context.HttpMethodAttribute!;
            var authenticationManager = context.AuthenticationManagerFactory?.Invoke();
            string finalRoute = httpMethod.Template;

            Dictionary<string, object> remainingArguments = HttpMessageHelper.GetRemainingArguments(request, context.Converter, ref finalRoute);

            url = HttpMessageHelper.BuildBodyAndFinalUrl(request, context, finalRoute, remainingArguments, ref content);
            
            var httpRequest = new HttpRequestMessage(httpMethod.HttpMethod, url);

            foreach (var header in await context.GetHeaders(context.ScopeServiceProvider))
                httpRequest.Headers.Add(header.Key, header.Value);

            if(content != null)
                httpRequest.Content = content;

            if (context.RequiresAuthentication && authenticationManager != null && authenticationManager.IsSessionActive)
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue(authenticationManager.TokenType!, authenticationManager.AccessToken);

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

            HubconResponse<bool> methodReponse = new HubconResponse<bool>(
                response.IsSuccessStatusCode,
                !response.IsSuccessStatusCode,
                "",
                response.Content.ToString(),
                (int)response.StatusCode,
                true
            );

            await context.SetResponse(methodReponse);

            content?.Dispose();
            httpRequest.Dispose();
            response.Dispose();
        }

        public override async ValueTask<IAsyncEnumerable<JsonElement>> GetStream(IOperationRequest request, IClientOperationContext context, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            StringContent? content = null;
            var url = context.HttpUrl;
            var methodInfo = (context.Member as MethodInfo)!;
            var httpMethod = context.HttpMethodAttribute!;
            var authenticationManager = context.AuthenticationManagerFactory?.Invoke();
            string finalRoute = httpMethod.Template;

            Dictionary<string, object> remainingArguments = HttpMessageHelper.GetRemainingArguments(request, context.Converter, ref finalRoute);
            url = HttpMessageHelper.BuildBodyAndFinalUrl(request, context, finalRoute, remainingArguments, ref content);


            var httpRequest = new HttpRequestMessage(httpMethod.HttpMethod, url);
            httpRequest.SetBrowserResponseStreamingEnabled(true);

            foreach (var header in await context.GetHeaders(context.ScopeServiceProvider))
                httpRequest.Headers.Add(header.Key, header.Value);

            if (content != null)
                httpRequest.Content = content;

            if (context.RequiresAuthentication && authenticationManager != null && authenticationManager.IsSessionActive)
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue(authenticationManager.TokenType!, authenticationManager.AccessToken);

            HttpResponseMessage response;
            response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            var enumerable = HttpMessageHelper.ParseSSEStream(response, context, cancellationToken);

            var methodReponse = new HubconResponse<IAsyncEnumerable<JsonElement>>(
                response.IsSuccessStatusCode,
                !response.IsSuccessStatusCode,
                "",
                "",
                (int)response.StatusCode,
                enumerable
            );

            await context.SetResponse(methodReponse);

            content?.Dispose();
            httpRequest.Dispose();

            return enumerable;
        }

        public override ValueTask Ingest<T>(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public override async ValueTask SendAsync<T>(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken = default)
        {
            StringContent? content = null;
            var url = "";
            var methodInfo = (context.Member as MethodInfo)!;
            var httpMethod = context.HttpMethodAttribute!;
            var authenticationManager = context.AuthenticationManagerFactory?.Invoke();
            var converter = context.Converter;
            var route = methodInfo.GetRoute(false);
            string finalRoute = httpMethod.Template;

            Dictionary<string, object> remainingArguments = HttpMessageHelper.GetRemainingArguments(request, converter, ref finalRoute);
            url = HttpMessageHelper.BuildBodyAndFinalUrl(request, context, finalRoute, remainingArguments, ref content);

            var httpRequest = new HttpRequestMessage(httpMethod.HttpMethod, url);

            foreach (var header in await context.GetHeaders(context.ScopeServiceProvider))
                httpRequest.Headers.Add(header.Key, header.Value);

            if (content != null)
                httpRequest.Content = content;     

            if (context.RequiresAuthentication && authenticationManager != null && authenticationManager.IsSessionActive)
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue(authenticationManager.TokenType!, authenticationManager.AccessToken);

            HttpResponseMessage response = await _httpClient.SendAsync(httpRequest, cancellationToken);

            var responseBytes = await response.Content.ReadAsByteArrayAsync();
            var result = converter.DeserializeByteArray<JsonElement>(responseBytes);

            var methodReponse = new HubconResponse<T>(
                response.IsSuccessStatusCode,
                !response.IsSuccessStatusCode,
                "",
                "",
                (int)response.StatusCode,
                context.Converter.DeserializeJsonElement<T>(result)
            );

            await context.SetResponse(methodReponse);

            content?.Dispose();
            httpRequest.Dispose();
            response.Dispose();
        }

        protected override void Build(TransportContext configuration)
        {
            _httpClient = configuration.ClientOptions.HttpClientFactory.Invoke(configuration.ProxyServiceProvider);
            configuration.ClientOptions.HttpClientOptions?.Invoke(_httpClient, configuration.ProxyServiceProvider);
        }
    }
}
