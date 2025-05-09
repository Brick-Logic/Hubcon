using System.Reflection;
using System.Text.Json;

namespace Hubcon.Core.Abstractions.Interfaces
{
    public interface IHubconClient
    {
        Task<IMethodResponse<JsonElement>> SendRequestAsync(IMethodInvokeRequest request, MethodInfo methodInfo, string resolver, CancellationToken cancellationToken = default);
        IAsyncEnumerable<JsonElement> GetStream(IMethodInvokeRequest request, string resolver, CancellationToken cancellationToken = default);
        IAsyncEnumerable<JsonElement> GetSubscription(ISubscriptionRequest request, string resolver, CancellationToken cancellationToken = default);
    }
}
