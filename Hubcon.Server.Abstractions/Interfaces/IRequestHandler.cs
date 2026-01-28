using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Standard.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Hubcon.Server.Abstractions.Interfaces
{
    public interface IRequestHandler
    {
        Task<IHubconResponse> GetStream(IOperationRequest request, TransportAttribute transportAttribute, object? wrappedRequest, CancellationToken cancellationToken = default);
        Task<IHubconResponse> GetSubscription(IOperationRequest request, TransportAttribute transportAttribute, CancellationToken cancellationToken = default);
        Task<IHubconResponse> HandleIngest(IOperationRequest request, TransportAttribute transportAttribute, Dictionary<Guid, object> sources, object? wrappedRequest, CancellationToken cancellationToken = default);
        Task<IHubconResponse> HandleSynchronous(IOperationRequest request, TransportAttribute transportAttribute, object? wrappedRequest, CancellationToken cancellationToken = default);
        Task<IHubconResponse> HandleSynchronousResult(IOperationRequest request, TransportAttribute transportAttribute, object? wrappedRequest, CancellationToken cancellationToken = default);
        Task<IHubconResponse> HandleWithoutResultAsync(IOperationRequest request, TransportAttribute transportAttribute, object? wrappedRequest, CancellationToken cancellationToken = default);
        Task<IHubconResponse> HandleWithResultAsync(IOperationRequest request, TransportAttribute transportAttribute, object? wrappedRequest, CancellationToken cancellationToken = default);
    }
}
