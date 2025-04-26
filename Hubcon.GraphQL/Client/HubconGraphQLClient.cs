using GraphQL;
using GraphQL.Client.Abstractions;
using GraphQL.Client.Http;
using Hubcon.Core.Converters;
using Hubcon.Core.Models;
using Hubcon.GraphQL.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace Hubcon.GraphQL.Client
{
    public class HubconGraphQLClient : IHubconGraphQLClient
    {
        private readonly GraphQLHttpClient _graphQLHttpClient;
        private readonly ILogger<HubconGraphQLClient> _logger;
        private readonly DynamicConverter converter;
        private readonly string _graphqlEndpoint;
        private readonly string _graphqlWebSocketEndpoint;

        public HubconGraphQLClient(
            GraphQLHttpClient graphQLHttpClient, 
            ILogger<HubconGraphQLClient> logger, 
            DynamicConverter converter,
            IConfiguration configuration)
        {
            _graphQLHttpClient = graphQLHttpClient;
            _logger = logger;
            this.converter = converter;
        }

        public async Task<BaseMethodResponse> SendRequestAsync(MethodInvokeRequest request, MethodInfo methodInfo, string resolver)
        {
            var craftedRequest = BuildRequest(request, methodInfo, "mutation", resolver);
            var response = await _graphQLHttpClient.SendMutationAsync<JsonElement>(craftedRequest);
            var result = response.Data.GetProperty(resolver);

            if(!result.TryGetProperty("success", out JsonElement successValue))
            {
                return new BaseMethodResponse(false);
            }

            result.TryGetProperty("data", out JsonElement dataValue);

            return new BaseMethodResponse(
                converter.DeserializeJsonElement<bool>(successValue),
                dataValue      
            );
        }

        public IAsyncEnumerable<JsonElement> SubscribeToMessages(MethodInvokeRequest request, MethodInfo methodInfo, string resolver)
        {
            // WebSocket connection (for subscriptions)
            var clientWebSocket = new ClientWebSocket();
            clientWebSocket.ConnectAsync(new Uri(_graphqlWebSocketEndpoint), CancellationToken.None).Wait();

            var subscriptionMessage = BuildRequest(request, methodInfo, "subscription", resolver);
            var jsonMessage = JsonSerializer.Serialize(subscriptionMessage);

            // Send the subscription message to the WebSocket server
            clientWebSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(jsonMessage)), WebSocketMessageType.Text, true, CancellationToken.None).Wait();

            var buffer = new byte[1024];
            var cts = new CancellationTokenSource();

            return GetWebSocketMessages(clientWebSocket, buffer, cts.Token);
        }

        public IAsyncEnumerable<JsonElement> SubscribeUsingSSE(MethodInvokeRequest request, MethodInfo methodInfo, string resolver)
        {
            // SSE logic
            var url = $"{_graphqlEndpoint}/events";
            var httpClient = new HttpClient();
            var response = httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);

            return ParseSseResponse(response.Result.Content.ReadAsStreamAsync().Result);
        }

        private async IAsyncEnumerable<JsonElement> GetWebSocketMessages(ClientWebSocket clientWebSocket, byte[] buffer, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var result = await clientWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var responseString = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseString);
                    if (jsonResponse.ValueKind != JsonValueKind.Null)
                        yield return jsonResponse;
                }
            }
        }

        private async IAsyncEnumerable<JsonElement> ParseSseResponse(Stream stream)
        {
            using var reader = new StreamReader(stream);
            string line;

            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (line.StartsWith("data:"))
                {
                    var json = line.Substring(5).Trim();
                    var jsonResponse = JsonSerializer.Deserialize<JsonElement>(json);
                    yield return jsonResponse!;
                }
            }
        }

        private GraphQLRequest BuildRequest(MethodInvokeRequest request, MethodInfo methodInfo, string requestType, string resolver)
        {
            var sb = new StringBuilder();

            sb.Append($"{requestType}(${nameof(request)}: {nameof(MethodInvokeRequest)}Input!) {{");
            sb.Append($"{resolver}({nameof(request)}: $request) {{");


            if(methodInfo.ReturnType == typeof(Task))
            {
                sb.Append($"success");
            }
            else
            {
                sb.Append($"data ");
                sb.Append($"success");
            }


            sb.Append("}}");

            var invokeRequest = new GraphQLRequest()
            {
                Query = sb.ToString(),
                Variables = new
                {
                    request
                },
            };

            return invokeRequest;
        }
    }

}
