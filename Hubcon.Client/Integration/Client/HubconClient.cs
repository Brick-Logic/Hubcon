using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;
using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Client.Core.MessageHandlers;
using Hubcon.Client.Core.Subscriptions;
using Hubcon.Client.Core.Websockets;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Core.Subscriptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace Hubcon.Client.Integration.Client
{
    public class HubconClient(
        IDynamicConverter converter,
        Lazy<HttpClient> clientFactory,
        ILogger<HubconClient> logger) : IHubconClient
    {
        private string _graphqlHttpUrl = "";
        private string _graphqlWebsocketUrl = "";

        private string _restHttpUrl = "";
        private string _websocketUrl = "";

        Func<IAuthenticationManager?>? authenticationManagerFactory;
        HubconWebSocketClient client;

        SubscriptionManager subscriptionManager;
        private Func<GraphQLHttpClientOptions> graphQLHttpClientOptionsFactory;
        private Func<GraphQLHttpClient> graphQLHttpClientFactory;
        private GraphQLHttpClientOptions? graphQLHttpClientOptions;
        private GraphQLHttpClient graphQLHttpClient;

        private bool IsStarted { get; set; }
        public async Task Start()
        {
            //return;

            //if (true || IsStarted) return;

            Task _runnerTask = Task.CompletedTask;
            Task _exceptionTask = Task.CompletedTask;


            //var runner = async () =>
            //{
            //    var sw = new Stopwatch();

            //    while (true)
            //    {
            //        try
            //        {
            //            if (authenticationManagerFactory!.Invoke()!.IsSessionActive)
            //            {
            //                IObservable<object?> observable = graphQLHttpClientFactory.Invoke().PongStream;
            //                var observer = new AsyncObserver<object?>();

            //                using (observable.Subscribe(observer))
            //                {
            //                    sw.Start();
            //                    //timer.Start();
            //                    await foreach (var newEvent in observer.GetAsyncEnumerable(new CancellationToken()))
            //                    {
            //                        sw.Restart();
            //                        logger.LogInformation($"PONG received.");
            //                    }
            //                }
            //            }

            //            await Task.Delay(millisecondsDelay: 1000);
            //        }
            //        catch (Exception ex)
            //        {
            //            if (ex.Message.Contains("Connection closed by the server"))
            //            {
            //                logger.LogInformation($"Error: El servidor cerró la conexión. Es posible que la sesión haya expirado o no se haya podido autenticar. Intentando refrescar la sesión...");
            //                var res = await authenticationManagerFactory!.Invoke()!.TryRefreshSessionAsync();
            //                if (!res.IsSuccess)
            //                {
            //                    logger.LogInformation($"Error: No se pudo refrescar la sesión.");

            //                }
            //                await Task.Delay(1000);
            //            }
            //        }
            //    }
            //};

            //_ = Task.Run(async () =>
            //{
            //    while (true)
            //    {
            //        try
            //        {
            //            while (true)
            //            {
            //                try
            //                {
            //                    await Task.Delay(millisecondsDelay: 1000);

            //                    if (authenticationManagerFactory!.Invoke()!.IsSessionActive)
            //                    {
            //                        if (_runnerTask == null || _runnerTask.IsCompleted)
            //                        {
            //                            _runnerTask = Task.Run(runner);
            //                        }

            //                        await graphQLHttpClientFactory.Invoke().SendPingAsync(null);
            //                    }
            //                }
            //                catch (Exception ex)
            //                {
            //                }
            //            }
            //        }
            //        catch (Exception ex)
            //        {
            //            logger.LogError($"Error: {ex.Message}");
            //        }
            //    }
            //}).ConfigureAwait(false);

            IsStarted = true;
        }

        public async Task<IOperationResponse<JsonElement>> SendRequestAsync(IOperationRequest request, MethodInfo methodInfo, string resolver, CancellationToken cancellationToken = default)
        {
            try
            {
                //HubconGraphQLRequest? craftedRequest = BuildRequest(request, methodInfo, resolver);
                var bytes = converter.SerializeObject(request).ToString();
                using var content = new StringContent(bytes, Encoding.UTF8, "application/json");

                var httpRequest = new HttpRequestMessage(HttpMethod.Post, _restHttpUrl + $"/{resolver}")
                {
                    Content = content
                };

                var authManager = authenticationManagerFactory?.Invoke();

                if (authManager != null && authManager.IsSessionActive)
                    httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authManager.AccessToken);


                var client = clientFactory.Value;
                var response = await client.SendAsync(httpRequest, cancellationToken);

                var responseBytes = await response.Content.ReadAsByteArrayAsync();
                var result = converter.DeserializeByteArray<JsonElement>(responseBytes);

                result.TryGetProperty(nameof(IObjectOperationResponse.Success).ToLower(), out JsonElement successValue);
                result.TryGetProperty(nameof(BaseOperationResponse.Data).ToLower(), out JsonElement dataValue);
                result.TryGetProperty(nameof(BaseOperationResponse.Error).ToLower(), out JsonElement errorValue);

                return new BaseJsonResponse(
                    converter.DeserializeJsonElement<bool>(successValue),
                    dataValue,
                    converter.DeserializeJsonElement<string>(errorValue)
                );
            }
            catch (Exception ex)
            {
                return new BaseJsonResponse(
                    false,
                    default,
                    ex.Message
                );
            }

        }

        public async IAsyncEnumerable<JsonElement> GetStream(IOperationRequest request, string resolver, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (!IsStarted)
                await Start();

            //var graphQLRequest = BuildStream(request, resolver);

            IObservable<JsonElement> observable = await client.Stream<JsonElement>(request);

            var observer = new AsyncObserver<JsonElement>();

            using (observable.Subscribe(observer))
            {
                await foreach (var newEvent in observer.GetAsyncEnumerable(cancellationToken))
                {
                    var result = newEvent!.Clone();
                    yield return result;
                }
            }
        }

        public async IAsyncEnumerable<JsonElement> GetSubscription(IOperationRequest request, string resolver, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (!IsStarted)
                await Start();

            if (authenticationManagerFactory?.Invoke() == null)
                throw new UnauthorizedAccessException("Subscriptions are required to be authenticated. Use 'UseAuthorizationManager()' extension method.");

            IObservable<JsonElement> observable = await client.Subscribe<JsonElement>(request);

            var observer = new AsyncObserver<JsonElement>();

            using (observable.Subscribe(observer))
            {
                await foreach (var newEvent in observer.GetAsyncEnumerable(cancellationToken))
                {
                    var result = newEvent!.Clone();
                    yield return result;
                }
            }
        }

        private HubconGraphQLRequest BuildRequest(IOperationRequest invokeRequest, MethodInfo methodInfo, string resolver)
        {
            var sb = new StringBuilder();
            var request = new OperationRequest(invokeRequest.OperationName, invokeRequest.ContractName, invokeRequest.Args);

            sb.Append($"mutation(${nameof(request)}: {nameof(OperationRequest)}Input!) {{");
            sb.Append($"{resolver}({nameof(request)}: $request) {{");


            if (methodInfo.ReturnType == typeof(Task) || methodInfo.ReturnType == typeof(void))
            {
                sb.Append($"success ");
                sb.Append($"error ");
            }
            else
            {
                sb.Append($"success ");
                sb.Append($"error ");
                sb.Append($"data ");
            }

            sb.Append("}}");

            var graphqlRequest = new HubconGraphQLRequest()
            {
                Query = sb.ToString()
            };

            graphqlRequest.Variables.Add($"{nameof(request)}", converter.SerializeObject(request));

            return graphqlRequest;
        }

        private GraphQLRequest BuildStream(IOperationRequest invokeRequest, string resolver)
        {
            var sb = new StringBuilder();

            var request = new OperationRequest(invokeRequest.OperationName, invokeRequest.ContractName, invokeRequest.Args);

            sb.Append($"subscription(${nameof(request)}: {nameof(OperationRequest)}Input!) {{");
            sb.Append($"{resolver}({nameof(request)}: $request) }}");

            var graphqlRequest = new GraphQLRequest()
            {
                Query = sb.ToString(),
                Variables = new
                {
                    request
                },
            };

            return graphqlRequest;
        }

        private bool IsBuilt { get; set; }
        public void Build(Uri BaseUri, string? HttpEndpoint, string? WebsocketEndpoint, Type? AuthenticationManagerType, IServiceProvider services, bool useSecureConnection = true)
        {
            if (IsBuilt) return;

            var baseHttpUrl = $"{BaseUri!.AbsoluteUri}/{HttpEndpoint ?? "graphql"}";
            var baseWebsocketUrl = $"{BaseUri!.AbsoluteUri}/{WebsocketEndpoint ?? "graphql"}";

            _graphqlHttpUrl = useSecureConnection ? $"https://{baseHttpUrl}" : $"http://{baseHttpUrl}";
            _graphqlWebsocketUrl = useSecureConnection ? $"wss://{baseWebsocketUrl}" : $"ws://{baseWebsocketUrl}";


            var baseRestHttpUrl = $"{BaseUri!.AbsoluteUri}/{HttpEndpoint ?? "operation"}";
            var baseRestWebsocketUrl = $"{BaseUri!.AbsoluteUri}/{WebsocketEndpoint ?? "ws"}";

            _restHttpUrl = useSecureConnection ? $"https://{baseRestHttpUrl}" : $"http://{baseRestHttpUrl}";
            _websocketUrl = useSecureConnection ? $"wss://{baseRestWebsocketUrl}" : $"ws://{baseRestWebsocketUrl}";

            if (AuthenticationManagerType is not null)
            {
                var lazyAuthType = typeof(Lazy<>).MakeGenericType(AuthenticationManagerType);
                authenticationManagerFactory = () => (IAuthenticationManager)((dynamic)services.GetRequiredService(lazyAuthType)).Value;
            }


            graphQLHttpClientOptionsFactory = () => graphQLHttpClientOptions ??= new GraphQLHttpClientOptions
            {
                EndPoint = new Uri(_graphqlHttpUrl),
                WebSocketEndPoint = new Uri(_graphqlWebsocketUrl),
                WebSocketProtocol = "graphql-transport-ws",
                HttpMessageHandler = new HttpClientMessageHandler(authenticationManagerFactory?.Invoke())
            };

            graphQLHttpClientFactory = () => graphQLHttpClient ??= new GraphQLHttpClient(graphQLHttpClientOptionsFactory.Invoke(), new SystemTextJsonSerializer(), clientFactory.Value);

            //subscriptionManager = new SubscriptionManager(new Uri(_graphqlWebsocketUrl), converter, authenticationManagerFactory, () => new ClientWebSocket());

            client = new HubconWebSocketClient(new Uri(_websocketUrl));

            //client.WebSocketOptions = (x) =>
            //{
            //    var authManager = authenticationManagerFactory?.Invoke();
            //    if (authManager is not null && authManager.IsSessionActive)
            //        x.SetRequestHeader("Authorization", $"Bearer {authManager?.AccessToken}");
            //};

            IsBuilt = true;
        }
    }
}
