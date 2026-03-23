using Hubcon.Shared.Core.Websockets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon
{
    /// <summary>
    /// Manages global rate limiting state across multiple transport layers and operations.
    /// Facilitates the mapping between persistent anchor keys (e.g., Session IDs) and 
    /// transient operation identifiers to ensure unified resource throttling.
    /// </summary>
    public interface IGlobalRateLimiterManager
    {
        /// <summary>
        /// Establishes a link between a persistent anchor key and a specific operation.
        /// Used by transport layers to track which resources are consumed by a given request.
        /// </summary>
        /// <param name="anchorKey">The unique string key representing the identity or session to throttle.</param>
        /// <param name="id">The unique <see cref="Guid"/> of the specific operation or connection.</param>
        /// <param name="transportAttribute">The transport protocol metadata associated with the operation.</param>
        /// <param name="request">The <see cref="IOperationRequest"/> containing the call details.</param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous linking operation.</returns>
        ValueTask Link(string anchorKey, Guid id, HubconTransportAttribute transportAttribute, IOperationRequest request);

        /// <summary>
        /// Asynchronously attempts to acquire permits for an operation based on its transport and message type.
        /// </summary>
        /// <param name="anchorKey">The persistent key to check against.</param>
        /// <param name="type">The <see cref="MessageType"/> (e.g., RoundTrip, Stream, Ingest) defining the cost.</param>
        /// <param name="transport">The transport protocol being used.</param>
        /// <param name="operation">Optional. The specific operation request for granular throttling.</param>
        /// <param name="permits">The number of permits to request. Defaults to 1.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to monitor for cancellation.</param>
        /// <returns>A <see cref="ValueTask{TResult}"/> that is <see langword="true"/> if permits were acquired; otherwise, <see langword="false"/>.</returns>
        ValueTask<bool> TryAcquireAsync(string anchorKey, MessageType type, HubconTransportAttribute transport, IOperationRequest? operation = null, int permits = 1, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously attempts to acquire permits for a specific resource ID.
        /// </summary>
        /// <param name="anchorKey">The persistent key to check against.</param>
        /// <param name="type">The <see cref="MessageType"/> defining the cost.</param>
        /// <param name="resourceId">The <see cref="Guid"/> of the specific resource or operation.</param>
        /// <param name="permits">The number of permits to request. Defaults to 1.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to monitor for cancellation.</param>
        /// <returns>A <see cref="ValueTask{TResult}"/> that is <see langword="true"/> if permits were acquired; otherwise, <see langword="false"/>.</returns>
        ValueTask<bool> TryAcquireAsync(string anchorKey, MessageType type, Guid resourceId, int permits = 1, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes the link between an anchor key and an operation ID, typically called when 
        /// a connection is closed or an operation completes.
        /// </summary>
        /// <param name="anchorKey">The persistent key the operation was linked to.</param>
        /// <param name="operationId">The <see cref="Guid"/> of the operation to unlink.</param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous unlinking operation.</returns>
        ValueTask Unlink(string anchorKey, Guid operationId);
    }
}
