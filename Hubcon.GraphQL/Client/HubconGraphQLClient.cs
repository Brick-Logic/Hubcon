using GraphQL;
using GraphQL.Client.Http;
using Hubcon.Core.Abstractions.Interfaces;
using Hubcon.Core.Invocation;
using Hubcon.Core.Subscriptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Timers;

namespace Hubcon.GraphQL.Client
{
    public class HubconGraphQLClient : IHubconClient
    {
        private readonly GraphQLHttpClient _graphQLHttpClient;
        private readonly ILogger<IHubconClient> _logger;
        private readonly IDynamicConverter _converter;

        public HubconGraphQLClient(
            GraphQLHttpClient graphQLHttpClient, 
            ILogger<HubconGraphQLClient> logger,
            IDynamicConverter converter,
            IConfiguration configuration)
        {
            _graphQLHttpClient = graphQLHttpClient;
            _logger = logger;
            _converter = converter;

            Task _runnerTask = Task.CompletedTask;

            var runner = async () =>
            {
                var sw = new Stopwatch();
                var timer = new System.Timers.Timer(5000);

                timer.Elapsed += async (object? sender, ElapsedEventArgs e) =>
                {
                    if (sw.ElapsedMilliseconds > 30000)
                    {
                        Console.WriteLine("Disconnected. Reconnecting...");
                        sw.Restart();
                        await _graphQLHttpClient.InitializeWebsocketConnection();
                    }
                };

                while (true)
                {
                    try
                    {
                        IObservable<object?> observable = _graphQLHttpClient.PongStream;
                        var observer = new AsyncObserver<object?>();

                        using (observable.Subscribe(observer))
                        {
                            sw.Start();
                            timer.Start();
                            await foreach (var newEvent in observer.GetAsyncEnumerable(new CancellationToken()))
                            {
                                sw.Restart();
                                Console.WriteLine($"PONG received. Payload: {newEvent}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error: {ex.Message}");
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
                                Console.WriteLine(ex.Message);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error: {ex.Message}");
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
            var craftedRequest = BuildRequest(request, methodInfo, resolver);
            var response = await _graphQLHttpClient.SendMutationAsync<JsonElement>(craftedRequest);
            var result = response.Data.GetProperty(resolver);

            if(!result.TryGetProperty(nameof(IObjectOperationResponse.Success).ToLower(), out JsonElement successValue))
            {
                return new BaseJsonResponse(false);
            }

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


            if(methodInfo.ReturnType == typeof(Task))
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

            var request = new SubscriptionRequest(invokeRequest.OperationName, invokeRequest.ContractName);

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

