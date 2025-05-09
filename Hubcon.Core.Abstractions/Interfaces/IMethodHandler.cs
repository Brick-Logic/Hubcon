using System.Text.Json;

namespace Hubcon.Core.Abstractions.Interfaces
{
    public interface IControllerInvocationHandler
    {
        public Task<IResponse> HandleSynchronous(IMethodInvokeRequest request);
        public Task<IResponse> HandleWithoutResultAsync(IMethodInvokeRequest request);

        public Task<IMethodResponse<JsonElement>> HandleSynchronousResult(IMethodInvokeRequest request);
        public Task<IMethodResponse<JsonElement>> HandleWithResultAsync(IMethodInvokeRequest request);

        public IAsyncEnumerable<JsonElement?> GetStream(IMethodInvokeRequest request);
        IAsyncEnumerable<JsonElement?> GetSubscription(ISubscriptionRequest request, CancellationToken cancellationToken = default);
    }
}
