using System.Reflection;
using System.Text.Json;

namespace Hubcon.Core.Abstractions.Interfaces
{
    public interface IHubconClient
    {
        Task<IOperationResponse<JsonElement>> SendRequestAsync(IOperationRequest request, MethodInfo methodInfo, string resolver, CancellationToken cancellationToken = default);
        IAsyncEnumerable<JsonElement> GetStream(IOperationRequest request, string resolver, CancellationToken cancellationToken = default);
        IAsyncEnumerable<JsonElement> GetSubscription(IOperationRequest request, string resolver, CancellationToken cancellationToken = default);
    }
}
