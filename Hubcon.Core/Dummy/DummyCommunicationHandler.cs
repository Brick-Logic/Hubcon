using Hubcon.Core.Abstractions.Interfaces;
using System.Reflection;
using System.Text.Json;

namespace Hubcon.Core.Dummy
{
    public class DummyCommunicationHandler : ICommunicationHandler
    {
        public Task CallAsync(IMethodInvokeRequest request, MethodInfo methodInfo, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<IMethodResponse<JsonElement>> InvokeAsync(IMethodInvokeRequest request, MethodInfo methodInfo, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<IAsyncEnumerable<T?>> StreamAsync<T>(IMethodInvokeRequest request, MethodInfo methodInfo, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
