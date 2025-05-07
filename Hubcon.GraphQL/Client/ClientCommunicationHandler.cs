using Hubcon.Core.Models;
using Hubcon.Core.Models.Exceptions;
using Hubcon.Core.Models.Interfaces;
using Hubcon.GraphQL.Models;
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
            try
            {
                var response = await _client.SendRequestAsync(request, methodInfo, nameof(IHubconEntrypoint.HandleMethodTask), cancellationToken);

                if (!response.Success)
                {
                    throw new HubconRemoteException($"Server message: {response.Data}");
                }

                return response;
            }
            catch (Exception ex)
            {
                // logging
                throw new HubconGenericException(ex.Message);
            }
        }

        public async Task CallAsync(MethodInvokeRequest request, MethodInfo methodInfo, CancellationToken cancellationToken)
        {
            try
            {
                var response = await _client.SendRequestAsync(request, methodInfo, nameof(IHubconEntrypoint.HandleMethodVoid), cancellationToken);

                if (!response.Success)
                {
                    throw new HubconRemoteException($"Server message: {response.Data}");
                }
            }
            catch (Exception ex)
            {
                // logging
                throw new HubconGenericException(ex.Message);
            }
        }

        public Task<IAsyncEnumerable<T?>> StreamAsync<T>(MethodInvokeRequest request, MethodInfo methodInfo, CancellationToken cancellationToken)
        {
            try
            {
                IAsyncEnumerable<JsonElement> stream;
                stream = _client.GetStream(request, nameof(IHubconEntrypoint.HandleMethodStream), cancellationToken);
                return Task.FromResult(_converter.ConvertStream<T?>(stream, cancellationToken));
            }
            catch (Exception ex)
            {
                // logging
                throw new HubconGenericException(ex.Message);
            }
        }
    }
}