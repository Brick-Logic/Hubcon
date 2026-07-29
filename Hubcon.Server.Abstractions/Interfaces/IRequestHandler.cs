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
    /// <summary>
    /// Defines the central engine for processing and dispatching all Hubcon operation types.
    /// This interface handles the transition from a raw transport request to an executed 
    /// service method, managing the lifecycle of streams, ingests, and standard RPC calls.
    /// </summary>
    public interface IRequestHandler
    {
        /// <summary>
        /// Processes a request to open an asynchronous data stream (IAsyncEnumerable).
        /// </summary>
        /// <param name="request">The metadata and arguments for the stream operation.</param>
        /// <param name="transportAttribute">The transport protocol being used.</param>
        /// <param name="wrappedRequest">The underlying transport-specific request object (e.g., HttpContext).</param>
        /// <param name="requestId">The ID of the request used for tracing.</param>
        /// <param name="cancellationToken">A token to monitor for operation cancellation.</param>
        /// <returns>A <see cref="Task"/> resulting in a <see cref="IHubconResponse"/> containing the stream reference.</returns>
        ValueTask<IResponse> GetStream(IOperationRequest request, HubconTransportAttribute transportAttribute, IWrapper? wrappedRequest, RequestId requestId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Handles high-throughput ingestion operations where multiple data sources are pushed to the server.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="transportAttribute"></param>
        /// <param name="sources"></param>
        /// <param name="wrappedRequest"></param>
        /// <param name="requestId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        ValueTask<IResponse> HandleIngest(IOperationRequest request, HubconTransportAttribute transportAttribute, Dictionary<Guid, object> sources, IWrapper? wrappedRequest, RequestId requestId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes an operation synchronously. Used for low-latency calls that bypass asynchronous scheduling overhead when possible.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="transportAttribute"></param>
        /// <param name="wrappedRequest"></param>
        /// <param name="requestId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        ValueTask<IResponse> HandleSynchronous(IOperationRequest request, HubconTransportAttribute transportAttribute, IWrapper? wrappedRequest, RequestId requestId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes an operation synchronously and returns the computed result.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="transportAttribute"></param>
        /// <param name="wrappedRequest"></param>
        /// <param name="requestId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        ValueTask<IResponse> HandleSynchronousResult(IOperationRequest request, HubconTransportAttribute transportAttribute, IWrapper? wrappedRequest, RequestId requestId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously executes a method that does not return a value (Task/void).
        /// </summary>
        /// <param name="request"></param>
        /// <param name="transportAttribute"></param>
        /// <param name="wrappedRequest"></param>
        /// <param name="requestId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        ValueTask<IResponse> HandleWithoutResultAsync(IOperationRequest request, HubconTransportAttribute transportAttribute, IWrapper? wrappedRequest, RequestId requestId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously executes a standard RPC method and returns the serialized result.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="transportAttribute"></param>
        /// <param name="wrappedRequest"></param>
        /// <param name="requestId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        ValueTask<IResponse> HandleWithResultAsync(IOperationRequest request, HubconTransportAttribute transportAttribute, IWrapper? wrappedRequest, RequestId requestId, CancellationToken cancellationToken = default);
    }
}
