using System.Text.Json;

namespace Hubcon.Core.Models.Interfaces
{
    public interface IControllerInvocationHandler
    {
        public Task<IResponse> HandleSynchronous(MethodInvokeRequest methodInfo);
        public Task<IResponse> HandleWithoutResultAsync(MethodInvokeRequest methodInfo);

        public Task<BaseJsonResponse> HandleSynchronousResult(MethodInvokeRequest methodInfo);
        public Task<BaseJsonResponse> HandleWithResultAsync(MethodInvokeRequest methodInfo);

        public IAsyncEnumerable<JsonElement?> GetStream(MethodInvokeRequest methodInfo);
    }
}
