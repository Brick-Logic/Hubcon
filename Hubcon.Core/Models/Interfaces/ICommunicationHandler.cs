using Hubcon.Core.Models;
using Hubcon.Core.Models.Interfaces;

namespace Hubcon.Core.Interfaces.Communication
{
    public interface ICommunicationHandler
    {
        public Task<MethodResponse> InvokeAsync(string method, object[] arguments, CancellationToken cancellationToken);
        public Task CallAsync(string method, object[] arguments, CancellationToken cancellationToken);
        public Task<IAsyncEnumerable<T>> StreamAsync<T>(string method, object[] arguments, CancellationToken cancellationToken);
        public List<IClientReference> GetAllClients();
        public ICommunicationHandler WithUserId(string id);
    }

    public interface IAsyncCommunicationHandler : ICommunicationHandler
    {
        public Task<MethodInvokeRequest> ReceiveAsync();
    }
}
