using Hubcon.Core.Abstractions.Interfaces;
using Hubcon.Core.Invocation;
using System.Reflection;
using System.Text.Json;

namespace Hubcon.Core.Dummy
{
    public class DummyServerCommunicationHandler : IServerCommunicationHandler
    {
        public Task CallAsync(IMethodInvokeRequest request, MethodInfo methodInfo, CancellationToken cancellationToken)
        {
            return Task.FromResult(0);
        }

        public List<IClientReference> GetAllClients()
        {
            return Array.Empty<IClientReference>().ToList();
        }

        public async Task<IMethodResponse<JsonElement>> InvokeAsync(IMethodInvokeRequest request, MethodInfo methodInfo, CancellationToken cancellationToken)
        {
            return await Task.FromResult(new BaseJsonResponse(true));
        }

        public Task<IAsyncEnumerable<T?>> StreamAsync<T>(IMethodInvokeRequest request, MethodInfo methodInfo, CancellationToken cancellationToken)
        {
            return Task.FromResult<IAsyncEnumerable<T?>>(null!);
        }

        public IServerCommunicationHandler WithClientId(string clientId)
        {
            return this;
        }
    }
}
