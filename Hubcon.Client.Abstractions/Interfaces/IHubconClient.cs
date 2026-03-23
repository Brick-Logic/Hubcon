using Hubcon.Shared.Abstractions.Interfaces;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Hubcon.Client.Abstractions.Interfaces
{
    /// <summary>
    /// Defines the primary client-side interface for interacting with a Hubcon server.
    /// Provides high-level methods for various communication patterns, including RPC, 
    /// fire-and-forget, streaming, and data ingestion.
    /// </summary>
    public interface IHubconClient
    {
        /// <summary>
        /// Asynchronously sends a request and waits for a typed response (Round-Trip).
        /// Used for standard RPC methods that return a value or a Task.
        /// </summary>
        /// <typeparam name="T">The expected type of the response data.</typeparam>
        /// <param name="request">The <see cref="IOperationRequest"/> containing method metadata and arguments.</param>
        /// <param name="context">The <see cref="IClientOperationContext"/> governing the execution lifecycle.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the response.</param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
        ValueTask SendAsync<T>(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken);

        /// <summary>
        /// Asynchronously executes a call without waiting for a server response (Fire-and-Forget).
        /// Used for void or Task-returning methods where result confirmation is not required.
        /// </summary>
        /// <param name="request">The <see cref="IOperationRequest"/> containing method metadata and arguments.</param>
        /// <param name="context">The <see cref="IClientOperationContext"/> governing the execution lifecycle.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while dispatching the call.</param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous dispatch.</returns>
        ValueTask CallAsync(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken);

        /// <summary>
        /// Establishes a real-time data stream and returns an asynchronous enumerable of JSON elements.
        /// </summary>
        /// <param name="request">The <see cref="IOperationRequest"/> initiating the stream subscription.</param>
        /// <param name="context">The <see cref="IClientOperationContext"/> governing the execution lifecycle.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe to terminate the stream connection.</param>
        /// <returns>A <see cref="ValueTask"/> containing an <see cref="IAsyncEnumerable{T}"/> of <see cref="JsonElement"/>.</returns>
        ValueTask<IAsyncEnumerable<JsonElement>> GetStream(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a high-throughput data ingestion operation, optimized for pushing large volumes 
        /// of data to the server with minimal protocol overhead.
        /// </summary>
        /// <typeparam name="T">The type of the data payload being ingested.</typeparam>
        /// <param name="request">The <see cref="IOperationRequest"/> associated with the ingestion endpoint.</param>
        /// <param name="context">The <see cref="IClientOperationContext"/> governing the execution lifecycle.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe during the ingestion process.</param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous ingestion.</returns>
        ValueTask Ingest<T>(IOperationRequest request, IClientOperationContext context, CancellationToken cancellationToken);
    }
}