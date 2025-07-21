using System.Text.Json;

namespace Hubcon.Shared.Abstractions.Interfaces
{
    public interface IRequestHandler
    {
        Task<IOperationResponse<IAsyncEnumerable<object?>?>> GetStream(IOperationRequest request, CancellationToken cancellationToken = default);
        Task<IOperationResponse<IAsyncEnumerable<object?>?>> GetSubscription(IOperationRequest request, CancellationToken cancellationToken = default);
        Task<IOperationResponse<JsonElement>> HandleIngest(IOperationRequest request, Dictionary<Guid, object> sources, CancellationToken cancellationToken = default);
        Task<IResponse> HandleSynchronous(IOperationRequest request, CancellationToken cancellationToken = default);
        Task<IOperationResponse<JsonElement>> HandleSynchronousResult(IOperationRequest request, CancellationToken cancellationToken = default);
        Task<IResponse> HandleWithoutResultAsync(IOperationRequest request, CancellationToken cancellationToken = default);
        Task<IOperationResponse<JsonElement>> HandleWithResultAsync(IOperationRequest request, CancellationToken cancellationToken = default);
    }
}