using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;
using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Client.Core.MessageHandlers;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Core.Subscriptions;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace Hubcon.Client.Integration.Client
{
    public class HubconClient : IHubconClient
    {
        private GraphQLHttpClient? _graphQLHttpClient;
        private IAuthenticationManager? _authenticationManager;
        private GraphQLHttpClientOptions? _graphQLHttpClientOptions;

        private Func<GraphQLHttpClient>? _graphQLHttpClientFactory;
        private Func<IAuthenticationManager?>? _authenticationManagerFactory;
        private Func<GraphQLHttpClientOptions>? _graphQLHttpClientOptionsFactory;

        private readonly ILogger<IHubconClient> _logger;
        private readonly IDynamicConverter _converter;

        public HubconClient(ILogger<HubconClient> logger, IDynamicConverter converter)
        {
            _logger = logger;
            _converter = converter;
        }

        private bool IsStarted { get; set; }
        public async Task Start()
        {
            if (IsStarted) return;

            Task _runnerTask = Task.CompletedTask;
            Task _exceptionTask = Task.CompletedTask;

            _graphQLHttpClientFactory!.Invoke().Options.ConfigureWebsocketOptions = (x) =>
            {
                var authManager = _authenticationManagerFactory?.Invoke();
                if (authManager is not null && authManager.IsSessionActive)
                    x.SetRequestHeader("Authorization", $"Bearer {authManager?.AccessToken}");
            };


            var runner = async () =>
            {
                var sw = new Stopwatch();

                while (true)
                {
                    try
                    {
                        IObservable<object?> observable = _graphQLHttpClientFactory!.Invoke().PongStream;
                        var observer = new AsyncObserver<object?>();

                        using (observable.Subscribe(observer))
                        {
                            sw.Start();
                            //timer.Start();
                            await foreach (var newEvent in observer.GetAsyncEnumerable(new CancellationToken()))
                            {
                                sw.Restart();
                                _logger.LogInformation($"PONG received.");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (ex.Message.Contains("Connection closed by the server"))
                        {
                            _logger.LogInformation($"Error: El servidor cerró la conexión. Es posible que la sesión haya expirado o no se haya podido autenticar. Intentando refrescar la sesión...");
                            var res = await _authenticationManager!.TryRefreshSessionAsync();
                            if (!res.IsSuccess)
                            {
                                _logger.LogInformation($"Error: No se pudo refrescar la sesión.");

                            }
                            await Task.Delay(1000);
                        }
                    }
                }
            };

            _ = Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        while (true)
                        {
                            try
                            {
                                await Task.Delay(millisecondsDelay: 1000);

                                if (_authenticationManagerFactory!.Invoke()!.IsSessionActive)
                                {
                                    if (_runnerTask == null || _runnerTask.IsCompleted)
                                    {
                                        _runnerTask = Task.Run(runner);
                                    }

                                    await _graphQLHttpClientFactory!.Invoke().SendPingAsync(null);
                                }
                            }
                            catch (Exception ex)
                            {
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error: {ex.Message}");
                    }
                }
            });

            IsStarted = true;
        }

        public async Task<IOperationResponse<JsonElement>> SendRequestAsync(IOperationRequest request, MethodInfo methodInfo, string resolver, CancellationToken cancellationToken = default)
        {
            if (!IsStarted)
                await Start();

            GraphQLRequest? craftedRequest = BuildRequest(request, methodInfo, resolver);
            var response = await _graphQLHttpClientFactory!.Invoke().SendMutationAsync<JsonElement>(craftedRequest);
            var result = response.Data.Clone().GetProperty(resolver);

            result.TryGetProperty(nameof(IObjectOperationResponse.Success).ToLower(), out JsonElement successValue);
            result.TryGetProperty(nameof(BaseOperationResponse.Data).ToLower(), out JsonElement dataValue);
            result.TryGetProperty(nameof(BaseOperationResponse.Error).ToLower(), out JsonElement errorValue);

            return new BaseJsonResponse(
                _converter.DeserializeJsonElement<bool>(successValue),
                dataValue,
                _converter.DeserializeJsonElement<string>(errorValue)
            );
        }

        public async IAsyncEnumerable<JsonElement> GetStream(IOperationRequest request, string resolver, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (!IsStarted)
                await Start();
            
            var graphQLRequest = BuildStream(request, resolver);
            IObservable<GraphQLResponse<JsonElement>> observable = _graphQLHttpClientFactory!.Invoke().CreateSubscriptionStream<JsonElement>(graphQLRequest);

            var observer = new AsyncObserver<GraphQLResponse<JsonElement>>();

            using (observable.Subscribe(observer))
            {
                await foreach (var newEvent in observer.GetAsyncEnumerable(cancellationToken))
                {
                    var result = newEvent!.Data.Clone().GetProperty(resolver).Clone();
                    yield return result;
                }
            }
        }

        public async IAsyncEnumerable<JsonElement> GetSubscription(IOperationRequest request, string resolver, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (!IsStarted)
                await Start();

            if (_authenticationManagerFactory?.Invoke() == null)
                throw new UnauthorizedAccessException("Subscriptions are required to be authenticated. Use 'UseAuthorizationManager()' extension method.");

            var graphQLRequest = BuildSubscription(request, resolver);
            IObservable<GraphQLResponse<JsonElement>> observable = _graphQLHttpClientFactory!.Invoke().CreateSubscriptionStream<JsonElement>(graphQLRequest);

            var observer = new AsyncObserver<GraphQLResponse<JsonElement>>();

            using (observable.Subscribe(observer))
            {
                await foreach (var newEvent in observer.GetAsyncEnumerable(cancellationToken))
                {
                    var result = newEvent!.Data.Clone().GetProperty(resolver);
                    yield return result;
                }
            }
        }

        private GraphQLRequest BuildRequest(IOperationRequest invokeRequest, MethodInfo methodInfo, string resolver)
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

        private GraphQLRequest BuildSubscription(IOperationRequest invokeRequest, string resolver)
        {
            var sb = new StringBuilder();

            var request = new SubscriptionRequest(invokeRequest.OperationName, invokeRequest.ContractName, null);

            sb.Append($"subscription(${nameof(request)}: {nameof(SubscriptionRequest)}Input!) {{");
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

            var httpUrl = useSecureConnection ? $"https://{baseHttpUrl}" : $"http://{baseHttpUrl}";
            var websocketUrl = useSecureConnection ? $"wss://{baseWebsocketUrl}" : $"ws://{baseWebsocketUrl}";

            Func<IAuthenticationManager?>? authManagerFactory = null;

            if (AuthenticationManagerType is not null)
            {
                authManagerFactory = () => (IAuthenticationManager)services.GetService(AuthenticationManagerType)!;
            }

            _authenticationManagerFactory = () =>
            {
                if (_authenticationManager != null)
                    return _authenticationManager;

                return _authenticationManager = authManagerFactory?.Invoke();
            };

            _graphQLHttpClientOptionsFactory = () =>
            {
                if (_graphQLHttpClientOptions != null)
                    return _graphQLHttpClientOptions;

                var options = new GraphQLHttpClientOptions
                {
                    EndPoint = new Uri(httpUrl),
                    WebSocketEndPoint = new Uri(websocketUrl),
                    WebSocketProtocol = "graphql-transport-ws",
                    HttpMessageHandler = new HttpClientMessageHandler(_authenticationManagerFactory.Invoke())
                };

                return _graphQLHttpClientOptions = options;
            };

            _graphQLHttpClientFactory = () =>
            {
                if (_graphQLHttpClient != null)
                    return _graphQLHttpClient;

                return _graphQLHttpClient = new GraphQLHttpClient(_graphQLHttpClientOptionsFactory.Invoke(), new SystemTextJsonSerializer());
            };

            IsBuilt = true;
        }
    }
}

