using Hubcon.Core.Abstractions.Interfaces;
using Hubcon.Core.Exceptions;
using Hubcon.Core.Invocation;
using System.Reflection;
using System.Text.Json;

namespace Hubcon.GraphQL.Client
{
    public class ClientCommunicationHandler : ICommunicationHandler
    {
        private readonly IHubconClient _client;
        private readonly IDynamicConverter _converter;

        public ClientCommunicationHandler(IHubconClient client, IDynamicConverter converter)
        {
            _client = client;
            _converter = converter;
        }

        public async Task<IMethodResponse<JsonElement>> InvokeAsync(IMethodInvokeRequest request, MethodInfo methodInfo, CancellationToken cancellationToken)
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
                throw new HubconGenericException(ex.Message, ex);
            }
        }

        public async Task CallAsync(IMethodInvokeRequest request, MethodInfo methodInfo, CancellationToken cancellationToken)
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
                throw new HubconGenericException(ex.Message);
            }
        }

        public Task<IAsyncEnumerable<T?>> StreamAsync<T>(IMethodInvokeRequest request, MethodInfo methodInfo, CancellationToken cancellationToken)
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