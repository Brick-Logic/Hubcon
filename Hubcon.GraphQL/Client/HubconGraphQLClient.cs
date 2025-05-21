using GraphQL;
using GraphQL.Client.Http;
using Hubcon.Core.Abstractions.Interfaces;
using Hubcon.Core.Authentication;
using Hubcon.Core.Invocation;
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
        private readonly GraphQLHttpClient _graphQLHttpClient;
        private readonly ILogger<IHubconClient> _logger;
        private readonly IDynamicConverter _converter;
        private readonly IAuthenticationManager? _authenticationManager;

        public HubconGraphQLClient(
            GraphQLHttpClient graphQLHttpClient, 
            ILogger<HubconGraphQLClient> logger,
            IDynamicConverter converter,
            IAuthenticationManager? authenticationManager = null)
        {
            _graphQLHttpClient = graphQLHttpClient;
            _logger = logger;
            _converter = converter;
            _authenticationManager = authenticationManager;
            Task _runnerTask = Task.CompletedTask;

            _graphQLHttpClient.Options.ConfigureWebsocketOptions = (x) =>
            {
                if (authenticationManager is not null && authenticationManager.IsSessionActive)
                    x.SetRequestHeader("Authorization", $"Bearer {authenticationManager?.AccessToken}");
            };

            var runner = async () =>
            {
                var sw = new Stopwatch();
                var timer1 = new System.Timers.Timer(5000);

                //timer.Elapsed += async (object? sender, ElapsedEventArgs e) =>
                //{
                //    try
                //    {
                //        if (sw.ElapsedMilliseconds > 30000)
                //        {
                //            logger.LogInformation("Disconnected. Reconnecting...");
                //            sw.Restart();
                //            //await _graphQLHttpClient.InitializeWebsocketConnection();
                //        }
                //    }
                //    finally
                //    {
                        
                //    }
                //};

                while (true)
                {
                    try
                    {
                        IObservable<object?> observable = _graphQLHttpClient.PongStream;
                        var observer = new AsyncObserver<object?>();

                        using (observable.Subscribe(observer))
                        {
                            sw.Start();
                            //timer.Start();
                            await foreach (var newEvent in observer.GetAsyncEnumerable(new CancellationToken()))
                            {
                                sw.Restart();
                                logger.LogInformation($"PONG received. Payload: {newEvent}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogInformation($"Error: {ex.Message}");
                    }
                }
            };

            _ = Task.Run(async () =>
            {
                if (_runnerTask == null || _runnerTask.IsCompleted)
                {
                    _runnerTask = Task.Run(runner);
                }

                while (true)
                {
                    try
                    {
                        while (true)
                        {
                            try
                            {
                                await Task.Delay(1000);
                                await _graphQLHttpClient.SendPingAsync("MyPayload");
                            }
                            catch(Exception ex)
                            {
                                logger.LogError(ex.Message);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError($"Error: {ex.Message}");
                        if (_runnerTask != null && !_runnerTask.IsCompleted)
                        {
                            _runnerTask.Dispose();
                        }
                        _runnerTask = Task.Run(runner);
                    }
                }
            });

        }

        public async Task<IOperationResponse<JsonElement>> SendRequestAsync(IOperationRequest request, MethodInfo methodInfo, string resolver, CancellationToken cancellationToken = default)
        {
          GraphQLRequest? craftedRequest = BuildRequest(request, methodInfo, resolver);
            var response = await _graphQLHttpClient.SendMutationAsync<JsonElement>(craftedRequest);
            var result = response.Data.GetProperty(resolver);

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
            var graphQLRequest = BuildStream(request, resolver);
            IObservable<GraphQLResponse<JsonElement>> observable = _graphQLHttpClient.CreateSubscriptionStream<JsonElement>(graphQLRequest);
           
            var observer = new AsyncObserver<GraphQLResponse<JsonElement>>();

            using (observable.Subscribe(observer))
            {
                await foreach (var newEvent in observer.GetAsyncEnumerable(cancellationToken))
                {
                     var result = newEvent!.Data.GetProperty(resolver).Clone();

                    yield return result;
                }
            }
        }

        public async IAsyncEnumerable<JsonElement> GetSubscription(IOperationRequest request, string resolver, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            //if(_authenticationManager == null)
            //    throw new UnauthorizedAccessException("Subscriptions are required to be authenticated. Use 'UseAuthorizationManager()' extension method.");

            var graphQLRequest = BuildSubscription(request, resolver);
            IObservable<GraphQLResponse<JsonElement>> observable = _graphQLHttpClient.CreateSubscriptionStream<JsonElement>(graphQLRequest);

            var observer = new AsyncObserver<GraphQLResponse<JsonElement>>();

            using (observable.Subscribe(observer))
            {
                await foreach (var newEvent in observer.GetAsyncEnumerable(cancellationToken))
                {
                    var result = newEvent!.Data.GetProperty(resolver);
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
    }
}

