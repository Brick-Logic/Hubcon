using Autofac;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;
using Hubcon.Core.Abstractions.Interfaces;
using Hubcon.Core.Authentication;
using Hubcon.Core.Invocation;
using Hubcon.Core.Middlewares.MessageHandlers;
using Hubcon.Core.Subscriptions;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace Hubcon.GraphQL.Client
{
    public class HubconGraphQLClient : IHubconClient
    {
        private GraphQLHttpClient? _graphQLHttpClient;
        private IAuthenticationManager? _authenticationManager;
        private GraphQLHttpClientOptions? _graphQLHttpClientOptions;

        private Func<GraphQLHttpClient>? _graphQLHttpClientFactory;
        private Func<IAuthenticationManager?>? _authenticationManagerFactory;
        private Func<GraphQLHttpClientOptions>? _graphQLHttpClientOptionsFactory;

        private readonly ILogger<IHubconClient> _logger;
        private readonly IDynamicConverter _converter;

        public HubconGraphQLClient(ILogger<HubconGraphQLClient> logger, IDynamicConverter converter)
        {
            _logger = logger;
            _converter = converter;
        }

        private bool IsStarted { get; set; }
        public void Start()
        {
            if(IsStarted) return;

            if (!_authenticationManagerFactory!.Invoke()!.IsSessionActive) return;

            Task _runnerTask = Task.CompletedTask;


            _graphQLHttpClientFactory!.Invoke().Options.ConfigureWebsocketOptions = (x) =>
            {
                var authManager = _authenticationManagerFactory?.Invoke();
                _logger.LogInformation("Intentando autenticar...");

                if (authManager is not null && authManager.IsSessionActive)
                    x.SetRequestHeader("Authorization", $"Bearer {authManager?.AccessToken}");         
            };

            var exceptionRunner = Task.Run(async () =>
            {
                var sw = new Stopwatch();
                var timer1 = new System.Timers.Timer(5000);

                while (true)
                {
                    try
                    {
                        IObservable<Exception> observable = _graphQLHttpClientFactory!.Invoke().WebSocketReceiveErrors;
                        var observer = new AsyncObserver<Exception>();

                        using (observable.Subscribe(observer))
                        {
                            sw.Start();
                            //timer.Start();
                            await foreach (var newEvent in observer.GetAsyncEnumerable(new CancellationToken()))
                            {
                                sw.Restart();
                                _logger.LogInformation($"Error received. Payload: {newEvent}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogInformation($"Error: {ex.Message}");
                    }
                }
            });

            var runner = async () =>
            {
                var sw = new Stopwatch();
                var timer1 = new System.Timers.Timer(5000);

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
                                _logger.LogInformation($"PONG received. Payload: {newEvent}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogInformation($"Error: {ex.Message}");
                    }
                }
            };

            _ = Task.Run(async () =>
            {
                if (_runnerTask == null || _runnerTask.IsCompleted)
                {
                    _runnerTask = Task.Run(runner);
                }

                exceptionRunner.Start();

                while (true)
                {
                    try
                    {

                        while (true)
                        {
                            try
                            {
                                await Task.Delay(1000);
                                await _graphQLHttpClientFactory!.Invoke().SendPingAsync("MyPayload");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError($"Error de websocket: {ex.Message}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error: {ex.Message}");
                        if (_runnerTask != null && !_runnerTask.IsCompleted)
                        {
                            _runnerTask.Dispose();
                        }
                        _runnerTask = Task.Run(runner);
                    }
                }
            });

            IsStarted = true;
        }

        public async Task<IOperationResponse<JsonElement>> SendRequestAsync(IOperationRequest request, MethodInfo methodInfo, string resolver, CancellationToken cancellationToken = default)
        {
            if (!IsStarted)
                Start();

            GraphQLRequest? craftedRequest = BuildRequest(request, methodInfo, resolver);
            var response = await _graphQLHttpClientFactory!.Invoke().SendMutationAsync<JsonElement>(craftedRequest);
            var result = response.Data.Clone().GetProperty(resolver);

            result.TryGetProperty(nameof(IObjectOperationResponse.Success).ToLower(), out JsonElement successValue);
            result.TryGetProperty(nameof(BaseOperationResponse.Data).ToLower(), out JsonElement dataValue);
            result.TryGetProperty(nameof(BaseOperationResponse.Error).ToLower(), out JsonElement errorValue);

            return new BaseJsonResponse(
                _converter.DeserializeJsonElement<bool>(successValue.Clone()),
                dataValue,
                _converter.DeserializeJsonElement<string>(errorValue.Clone())
            );
        }

        public async IAsyncEnumerable<JsonElement> GetStream(IOperationRequest request, string resolver, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (!IsStarted)
                Start();

            var graphQLRequest = BuildStream(request, resolver);
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

        public async IAsyncEnumerable<JsonElement> GetSubscription(IOperationRequest request, string resolver, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (!IsStarted)
                Start();

            if(_authenticationManagerFactory?.Invoke() == null)
                throw new UnauthorizedAccessException("Subscriptions are required to be authenticated. Use 'UseAuthorizationManager()' extension method.");

            var graphQLRequest = BuildSubscription(request, resolver);
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

        private GraphQLRequest BuildRequest(IOperationRequest invokeRequest, MethodInfo methodInfo, string resolver)
        {
            var sb = new StringBuilder();
            var request = new MethodInvokeRequest(invokeRequest.OperationName, invokeRequest.ContractName, invokeRequest.Args);

            sb.Append($"mutation(${nameof(request)}: {nameof(MethodInvokeRequest)}Input!) {{");
            sb.Append($"{resolver}({nameof(request)}: $request) {{");


            if(methodInfo.ReturnType == typeof(Task) || methodInfo.ReturnType == typeof(void))
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

            var request = new MethodInvokeRequest(invokeRequest.OperationName, invokeRequest.ContractName, invokeRequest.Args);

            sb.Append($"subscription(${nameof(request)}: {nameof(MethodInvokeRequest)}Input!) {{");
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
        public void Build(Uri BaseUri, string? HttpEndpoint, string? WebsocketEndpoint, Type? AuthenticationManagerType, IComponentContext context, bool useSecureConnection = true)
        {
            if (IsBuilt) return;

            var baseHttpUrl = $"{BaseUri!.AbsoluteUri}/{HttpEndpoint ?? "graphql"}";
            var baseWebsocketUrl = $"{BaseUri!.AbsoluteUri}/{WebsocketEndpoint ?? "graphql"}";

            var httpUrl = useSecureConnection ? $"https://{baseHttpUrl}" : $"http://{baseHttpUrl}";
            var websocketUrl = useSecureConnection ? $"wss://{baseWebsocketUrl}" : $"ws://{baseWebsocketUrl}";


            Func<IAuthenticationManager?>? authManagerFactory = null;

            if (AuthenticationManagerType is not null)
            {
                var type = typeof(Func<>).MakeGenericType(AuthenticationManagerType);
                authManagerFactory = (Func<IAuthenticationManager>?)context.ResolveOptional(type)!;
            }

            _authenticationManagerFactory = () =>
            {
                if(_authenticationManager != null)
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
                    HttpMessageHandler = new HttpClientMessageHandler((IAuthenticationManager?)_authenticationManagerFactory.Invoke())
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

