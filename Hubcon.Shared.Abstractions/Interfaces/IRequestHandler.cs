using System.Text.Json;

namespace Hubcon.Shared.Abstractions.Interfaces
{
    public interface IRequestHandler
    {
        public Task<IResponse> HandleSynchronous(IOperationRequest request);
        public Task<IResponse> HandleWithoutResultAsync(IOperationRequest request);

        public Task<IOperationResponse<JsonElement>> HandleSynchronousResult(IOperationRequest request);
        public Task<IOperationResponse<JsonElement>> HandleWithResultAsync(IOperationRequest request);

        public Task<IAsyncEnumerable<object?>> GetStream(IOperationRequest request);
        public Task<IAsyncEnumerable<object?>> GetSubscription(IOperationRequest request, CancellationToken cancellationToken = default);
    }
}
