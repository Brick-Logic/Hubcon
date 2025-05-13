using System.Reflection;
using System.Text.Json;

namespace Hubcon.Core.Abstractions.Interfaces
{
    public interface ICommunicationHandler
    {
        public Task<IOperationResponse<JsonElement>> InvokeAsync(IOperationRequest request, MethodInfo methodInfo, CancellationToken cancellationToken);
        public Task CallAsync(IOperationRequest request, MethodInfo methodInfo, CancellationToken cancellationToken);
        public Task<IAsyncEnumerable<T?>> StreamAsync<T>(IOperationRequest request, MethodInfo methodInfo, CancellationToken cancellationToken);
    }

    public interface IServerCommunicationHandler : ICommunicationHandler
    {
        public List<IClientReference> GetAllClients();
        public IServerCommunicationHandler WithClientId(string clientId);
    }
}
