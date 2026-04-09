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
        /// <param name="cancellationToken">A token to monitor for operation cancellation.</param>
        /// <returns>A <see cref="Task"/> resulting in a <see cref="IHubconResponse"/> containing the stream reference.</returns>
        ValueTask<IHubconResponse> GetStream(IOperationRequest request, HubconTransportAttribute transportAttribute, object? wrappedRequest, CancellationToken cancellationToken = default);

        /// <summary>
        /// Handles high-throughput ingestion operations where multiple data sources are pushed to the server.
        /// </summary>
        /// <param name="sources">A dictionary mapping source identifiers to their respective data objects.</param>
        ValueTask<IHubconResponse> HandleIngest(IOperationRequest request, HubconTransportAttribute transportAttribute, Dictionary<Guid, object> sources, object? wrappedRequest, CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes an operation synchronously. Used for low-latency calls that bypass 
        /// asynchronous scheduling overhead when possible.
        /// </summary>
        ValueTask<IHubconResponse> HandleSynchronous(IOperationRequest request, HubconTransportAttribute transportAttribute, object? wrappedRequest, CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes an operation synchronously and returns the computed result.
        /// </summary>
        ValueTask<IHubconResponse> HandleSynchronousResult(IOperationRequest request, HubconTransportAttribute transportAttribute, object? wrappedRequest, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously executes a method that does not return a value (Task/void).
        /// </summary>
        ValueTask<IHubconResponse> HandleWithoutResultAsync(IOperationRequest request, HubconTransportAttribute transportAttribute, object? wrappedRequest, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously executes a standard RPC method and returns the serialized result.
        /// </summary>
        ValueTask<IHubconResponse> HandleWithResultAsync(IOperationRequest request, HubconTransportAttribute transportAttribute, object? wrappedRequest, CancellationToken cancellationToken = default);
    }
}
