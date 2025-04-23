using Hubcon.Core.Models;

namespace Hubcon.Core.Models.Interfaces
{
    public interface ICommunicationHandler
    {
        public Task<IMethodResponse> InvokeAsync(MethodInvokeRequest request, CancellationToken cancellationToken);
        public Task CallAsync(MethodInvokeRequest request, CancellationToken cancellationToken);
        public Task<IAsyncEnumerable<T?>> StreamAsync<T>(MethodInvokeRequest request, CancellationToken cancellationToken);
    }

    public interface IServerCommunicationHandler : ICommunicationHandler
    {
        public List<IClientReference> GetAllClients();
        public IServerCommunicationHandler WithClientId(string clientId);
    }
}
