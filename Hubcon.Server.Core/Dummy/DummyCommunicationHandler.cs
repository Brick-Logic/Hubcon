using Hubcon.Shared.Abstractions.Interfaces;
using System.Reflection;
using System.Text.Json;

namespace Hubcon.Server.Core.Dummy
{
    public class DummyCommunicationHandler : ICommunicationHandler
    {
        public Task CallAsync(IOperationRequest request, MethodInfo methodInfo, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<IOperationResponse<JsonElement>> InvokeAsync(IOperationRequest request, MethodInfo methodInfo, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<IAsyncEnumerable<T?>> StreamAsync<T>(IOperationRequest request, MethodInfo methodInfo, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
