using System.Text.Json;

namespace Hubcon.Core.Abstractions.Interfaces
{
    public interface IRequestHandler
    {
        public Task<IResponse> HandleSynchronous(IOperationRequest request);
        public Task<IResponse> HandleWithoutResultAsync(IOperationRequest request);

        public Task<IOperationResponse<JsonElement>> HandleSynchronousResult(IOperationRequest request);
        public Task<IOperationResponse<JsonElement>> HandleWithResultAsync(IOperationRequest request);

        public IAsyncEnumerable<JsonElement?> GetStream(IOperationRequest request);
        IAsyncEnumerable<JsonElement?> GetSubscription(IOperationRequest request, CancellationToken cancellationToken = default);
    }
}
