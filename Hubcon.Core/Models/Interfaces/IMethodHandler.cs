using System.Text.Json;

namespace Hubcon.Core.Models.Interfaces
{
    public interface IControllerInvocationHandler
    {
        public Task<IResponse> HandleSynchronous(MethodInvokeRequest request);
        public Task<IResponse> HandleWithoutResultAsync(MethodInvokeRequest request);

        public Task<BaseJsonResponse> HandleSynchronousResult(MethodInvokeRequest request);
        public Task<BaseJsonResponse> HandleWithResultAsync(MethodInvokeRequest request);

        public IAsyncEnumerable<JsonElement?> GetStream(MethodInvokeRequest request);
        IAsyncEnumerable<JsonElement?> GetSubscription(SubscriptionRequest request, CancellationToken cancellationToken = default);
    }
}
