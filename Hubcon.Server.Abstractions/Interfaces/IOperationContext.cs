using Hubcon.Server.Abstractions.Delegates;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Hubcon
{
    /// <summary>
    /// Encapsulates all transport-agnostic and transport-specific information 
    /// for a single Hubcon operation lifecycle.
    /// </summary>
    public interface IOperationContext
    {
        /// <summary>Gets the compiled metadata and execution plan for the current operation.</summary>
        IOperationBlueprint Blueprint { get; init; }

        /// <summary>Gets or sets any exception encountered during the middleware or handler execution.</summary>
        Exception? Exception { get; set; }

        /// <summary>Gets the underlying HTTP context if the request originated via the HTTP transport.</summary>
        HttpContext? HttpContext { get; init; }

        /// <summary>Gets a key/value collection that can be used to share data between middleware components.</summary>
        IDictionary<string, object> Items { get; }

        /// <summary>Gets the unique name of the operation being executed (e.g., "Contract.Method").</summary>
        string OperationName { get; init; }

        /// <summary>Gets the raw request data, including arguments and header metadata.</summary>
        IOperationRequest Request { get; init; }

        /// <summary>Gets the transport-specific wrapper (e.g., the specific WebSocket connection instance).</summary>
        object? WrappedRequest { get; init; }

        /// <summary>Gets a cancellation token that triggers when the client disconnects or the request times out.</summary>
        CancellationToken RequestAborted { get; init; }

        /// <summary>Gets the scoped service provider for the current request.</summary>
        IServiceProvider RequestServices { get; init; }

        /// <summary>Gets or sets the response to be sent back to the client.</summary>
        IHubconResponse Response { get; set; }

        /// <summary>Gets or sets the security principal (identity) associated with the request.</summary>
        ClaimsPrincipal? User { get; set; }

        /// <summary>Indicates if this context is originated from a transport layer. If this property is True, the context will only be partially initialized.</summary>
        bool IsTransportCalled { get; }

        /// <summary>Handles the pipeline result according to the expected output.</summary>
        ResultHandlerDelegate ResultHandler { get; }
    }
}