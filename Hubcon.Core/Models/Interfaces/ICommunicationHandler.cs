using Hubcon.Core.Models;
using System.Reflection;

namespace Hubcon.Core.Models.Interfaces
{
    public interface ICommunicationHandler
    {
        public Task<IMethodResponse> InvokeAsync(MethodInvokeRequest request, MethodInfo methodInfo, CancellationToken cancellationToken);
        public Task CallAsync(MethodInvokeRequest request, MethodInfo methodInfo, CancellationToken cancellationToken);
        public Task<IAsyncEnumerable<T?>> StreamAsync<T>(MethodInvokeRequest request, MethodInfo methodInfo, CancellationToken cancellationToken);
    }

    public interface IServerCommunicationHandler : ICommunicationHandler
    {
        public List<IClientReference> GetAllClients();
        public IServerCommunicationHandler WithClientId(string clientId);
    }
}
