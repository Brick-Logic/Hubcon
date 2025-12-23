using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Hubcon.Shared.Abstractions.Interfaces
{
    public interface IRequestHandler
    {
        Task<IOperationResponse<IAsyncEnumerable<object?>?>> GetStream(IOperationRequest request, object? wrappedRequest, CancellationToken cancellationToken = default);
        Task<IOperationResponse<IAsyncEnumerable<object?>?>> GetSubscription(IOperationRequest request, CancellationToken cancellationToken = default);
        Task<IOperationResponse<JsonElement>> HandleIngest(IOperationRequest request, Dictionary<Guid, object> sources, object? wrappedRequest, CancellationToken cancellationToken = default);
        Task<IResponse> HandleSynchronous(IOperationRequest request, object? wrappedRequest, CancellationToken cancellationToken = default);
        Task<IOperationResponse<JsonElement>> HandleSynchronousResult(IOperationRequest request, object? wrappedRequest, CancellationToken cancellationToken = default);
        Task<IResponse> HandleWithoutResultAsync(IOperationRequest request, object? wrappedRequest, CancellationToken cancellationToken = default);
        Task<IOperationResponse<JsonElement>> HandleWithResultAsync(IOperationRequest request, object? wrappedRequest, CancellationToken cancellationToken = default);
    }
}