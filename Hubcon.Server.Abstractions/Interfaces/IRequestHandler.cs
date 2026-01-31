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
        Task<IHubconResponse> GetStream(IOperationRequest request, HubconTransportAttribute transportAttribute, object? wrappedRequest, CancellationToken cancellationToken = default);
        Task<IHubconResponse> GetSubscription(IOperationRequest request, HubconTransportAttribute transportAttribute, CancellationToken cancellationToken = default);
        Task<IHubconResponse> HandleIngest(IOperationRequest request, HubconTransportAttribute transportAttribute, Dictionary<Guid, object> sources, object? wrappedRequest, CancellationToken cancellationToken = default);
        Task<IHubconResponse> HandleSynchronous(IOperationRequest request, HubconTransportAttribute transportAttribute, object? wrappedRequest, CancellationToken cancellationToken = default);
        Task<IHubconResponse> HandleSynchronousResult(IOperationRequest request, HubconTransportAttribute transportAttribute, object? wrappedRequest, CancellationToken cancellationToken = default);
        Task<IHubconResponse> HandleWithoutResultAsync(IOperationRequest request, HubconTransportAttribute transportAttribute, object? wrappedRequest, CancellationToken cancellationToken = default);
        Task<IHubconResponse> HandleWithResultAsync(IOperationRequest request, HubconTransportAttribute transportAttribute, object? wrappedRequest, CancellationToken cancellationToken = default);
    }
}
