using System.Reflection;
using System.Text.Json;

namespace Hubcon.Core.Abstractions.Interfaces
{
    public interface ICommunicationHandler
    {
        public Task<IMethodResponse<JsonElement>> InvokeAsync(IMethodInvokeRequest request, MethodInfo methodInfo, CancellationToken cancellationToken);
        public Task CallAsync(IMethodInvokeRequest request, MethodInfo methodInfo, CancellationToken cancellationToken);
        public Task<IAsyncEnumerable<T?>> StreamAsync<T>(IMethodInvokeRequest request, MethodInfo methodInfo, CancellationToken cancellationToken);
    }

    public interface IServerCommunicationHandler : ICommunicationHandler
    {
        public List<IClientReference> GetAllClients();
        public IServerCommunicationHandler WithClientId(string clientId);
    }
}
