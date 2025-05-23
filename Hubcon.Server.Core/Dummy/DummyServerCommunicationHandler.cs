using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Core.Invocation;
using System.Reflection;
using System.Text.Json;

namespace Hubcon.Server.Core.Dummy
{
    public class DummyServerCommunicationHandler : IServerCommunicationHandler
    {
        public Task CallAsync(IOperationRequest request, MethodInfo methodInfo, CancellationToken cancellationToken)
        {
            return Task.FromResult(0);
        }

        public List<IClientReference> GetAllClients()
        {
            return Array.Empty<IClientReference>().ToList();
        }

        public async Task<IOperationResponse<JsonElement>> InvokeAsync(IOperationRequest request, MethodInfo methodInfo, CancellationToken cancellationToken)
        {
            return await Task.FromResult(new BaseJsonResponse(true));
        }

        public Task<IAsyncEnumerable<T?>> StreamAsync<T>(IOperationRequest request, MethodInfo methodInfo, CancellationToken cancellationToken)
        {
            return Task.FromResult<IAsyncEnumerable<T?>>(null!);
        }

        public IServerCommunicationHandler WithClientId(string clientId)
        {
            return this;
        }
    }
}
