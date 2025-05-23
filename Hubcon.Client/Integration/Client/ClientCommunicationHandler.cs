using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Client.Core.Exceptions;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Components.Invocation;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Text.Json;

namespace Hubcon.Client.Integration.Client
{
    public class ClientCommunicationHandler(
        IHubconClient client, 
        IDynamicConverter converter,
        ILogger<ClientCommunicationHandler> logger
        ) : ICommunicationHandler
    {
        public async Task<IOperationResponse<JsonElement>> InvokeAsync(IOperationRequest request, MethodInfo methodInfo, CancellationToken cancellationToken)
        {
            try
            {
                var response = await client.SendRequestAsync(request, methodInfo, nameof(IHubconEntrypoint.HandleMethodTask), cancellationToken);

                if (!response.Success)
                {
                    throw new HubconRemoteException($"Server message: {response.Error}");
                }

                return response;
            }
            catch (Exception ex)
            {
                logger.LogError(ex.Message);
                return null!;
            }
        }

        public async Task CallAsync(IOperationRequest request, MethodInfo methodInfo, CancellationToken cancellationToken)
        {
            try
            {
                var response = await client.SendRequestAsync(request, methodInfo, nameof(IHubconEntrypoint.HandleMethodVoid), cancellationToken);

                if (!response.Success)
                {
                    throw new HubconRemoteException($"Server message: {response.Error}");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex.Message);
            }
        }

        public Task<IAsyncEnumerable<T?>> StreamAsync<T>(IOperationRequest request, MethodInfo methodInfo, CancellationToken cancellationToken)
        {
            try
            {
                IAsyncEnumerable<JsonElement> stream;
                stream = client.GetStream(request, nameof(IHubconEntrypoint.HandleMethodStream), cancellationToken);
                return Task.FromResult(converter.ConvertStream<T?>(stream, cancellationToken));
            }
            catch (Exception ex)
            {
                // logging
                logger.LogError(ex.Message);
                return null!;
            }
        }
    }
}