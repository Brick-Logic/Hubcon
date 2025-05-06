using GraphQL;
using GraphQL.Client.Http;
using Hubcon.Core.Models;
using Hubcon.Core.Models.Interfaces;
using Hubcon.GraphQL.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Reactive.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace Hubcon.GraphQL.Client
{
    public class HubconGraphQLClient : IHubconGraphQLClient
    {
        private readonly GraphQLHttpClient _graphQLHttpClient;
        private readonly ILogger<IHubconGraphQLClient> _logger;
        private readonly IDynamicConverter converter;

        public HubconGraphQLClient(
            GraphQLHttpClient graphQLHttpClient, 
            ILogger<HubconGraphQLClient> logger,
            IDynamicConverter converter,
            IConfiguration configuration)
        {
            _graphQLHttpClient = graphQLHttpClient;
            _logger = logger;
            this.converter = converter;
        }

        public async Task<BaseMethodResponse> SendRequestAsync(MethodInvokeRequest request, MethodInfo methodInfo, string resolver, CancellationToken cancellationToken = default)
        {
            var craftedRequest = BuildRequest(request, methodInfo, resolver);
            var response = await _graphQLHttpClient.SendMutationAsync<JsonElement>(craftedRequest);
            var result = response.Data.GetProperty(resolver);

            if(!result.TryGetProperty(nameof(BaseMethodResponse.Success).ToLower(), out JsonElement successValue))
            {
                return new BaseMethodResponse(false);
            }

            result.TryGetProperty(nameof(BaseMethodResponse.Data).ToLower(), out JsonElement dataValue);

            return new BaseMethodResponse(
                converter.DeserializeJsonElement<bool>(successValue),
                dataValue      
            );
        }

        public async IAsyncEnumerable<JsonElement> GetStream(MethodInvokeRequest request, string resolver, [EnumeratorCancellation] CancellationToken cancellationToken = default)
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

        public async IAsyncEnumerable<JsonElement> GetSubscription(SubscriptionRequest request, string resolver, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var graphQLRequest = BuildSubscription(request, resolver);
            IObservable<GraphQLResponse<JsonElement>> observable = _graphQLHttpClient.CreateSubscriptionStream<JsonElement>(graphQLRequest);

            var observer = new AsyncObserver<GraphQLResponse<JsonElement>>();

            using (observable.Subscribe(observer))
            {
                await foreach (var newEvent in observer.GetAsyncEnumerable(cancellationToken))
                {
                    var result = newEvent.Data.GetProperty(resolver);

                    yield return result;
                }
            }
        }

        private GraphQLRequest BuildRequest(MethodInvokeRequest request, MethodInfo methodInfo, string resolver)
        {
            var sb = new StringBuilder();

            sb.Append($"mutation(${nameof(request)}: {nameof(MethodInvokeRequest)}Input!) {{");
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

        private GraphQLRequest BuildStream(MethodInvokeRequest request, string resolver)
        {
            var sb = new StringBuilder();

            sb.Append($"subscription(${nameof(request)}: {nameof(MethodInvokeRequest)}Input!) {{");
            sb.Append($"{resolver}({nameof(request)}: $request) }}");

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

        private GraphQLRequest BuildSubscription(SubscriptionRequest request, string resolver)
        {
            var sb = new StringBuilder();

            sb.Append($"subscription(${nameof(request)}: {nameof(SubscriptionRequest)}Input!) {{");
            sb.Append($"{resolver}({nameof(request)}: $request) }}");

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

