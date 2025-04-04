using Hubcon.Core.Models;
using Hubcon.Core.Models.Interfaces;

namespace Hubcon.Core.Interfaces.Communication
{
    public interface ICommunicationHandler
    {
        public Task<MethodResponse> InvokeAsync(MethodInvokeRequest request, CancellationToken cancellationToken);
        public Task CallAsync(MethodInvokeRequest request, CancellationToken cancellationToken);
        public Task<IAsyncEnumerable<T?>> StreamAsync<T>(MethodInvokeRequest request, CancellationToken cancellationToken);
    }

    public interface IServerCommunicationHandler : ICommunicationHandler
    {
        public List<IClientReference> GetAllClients();
        public IServerCommunicationHandler WithClientId(string clientId);
    }
}
