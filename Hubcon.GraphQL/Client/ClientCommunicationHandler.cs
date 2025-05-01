using Hubcon.Core.Converters;
using Hubcon.Core.Extensions;
using Hubcon.Core.Models;
using Hubcon.Core.Models.Interfaces;
using Hubcon.GraphQL.Models;
using Hubcon.GraphQL.Subscriptions;
using System.Reflection;
using System.Text.Json;

namespace Hubcon.SignalR.Client
{
    public class ClientCommunicationHandler : ICommunicationHandler
    {
        private readonly IHubconGraphQLClient _client;
        private readonly IDynamicConverter _converter;

        public ClientCommunicationHandler(IHubconGraphQLClient client, IDynamicConverter converter)
        {
            _client = client;
            _converter = converter;
        }

        public async Task<IMethodResponse> InvokeAsync(MethodInvokeRequest request, MethodInfo methodInfo, CancellationToken cancellationToken)
        {

            var response = await _client.SendRequestAsync(request, methodInfo, nameof(IHubconEntrypoint.HandleMethodTask));
            return response;
        }

        public async Task CallAsync(MethodInvokeRequest request, MethodInfo methodInfo, CancellationToken cancellationToken)
        {
            _ = await _client.SendRequestAsync(request, methodInfo, nameof(IHubconEntrypoint.HandleMethodVoid));
        }

        public Task<IAsyncEnumerable<T?>> StreamAsync<T>(MethodInvokeRequest request, MethodInfo methodInfo, CancellationToken cancellationToken)
        {
            IAsyncEnumerable<JsonElement> stream;
            stream = _client.GetStream(request, nameof(IHubconEntrypoint.HandleMethodStream), cancellationToken);
            return Task.FromResult(_converter.ConvertStream<T?>(stream, cancellationToken));
        }
    }
}