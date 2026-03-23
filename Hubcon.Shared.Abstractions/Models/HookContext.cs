using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Standard.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Hubcon.Shared.Abstractions.Models
{
    /// <summary>
    /// Defines the runtime execution state for a single Hubcon operation.
    /// Acts as a shared container for request data, dependency injection services, 
    /// and result tracking during the invocation lifecycle.
    /// </summary>
    public interface IInvocationContext
    {
        /// <summary>
        /// Gets the <see cref="IServiceProvider"/> for the current execution scope, 
        /// allowing for the resolution of scoped dependencies.
        /// </summary>
        public IServiceProvider Services { get; }

        /// <summary>
        /// Gets the raw <see cref="IOperationRequest"/> containing the method 
        /// signature and serialized arguments.
        /// </summary>
        public IOperationRequest Request { get; }

        /// <summary>
        /// Gets the <see cref="CancellationToken"/> that monitors the lifecycle 
        /// of this specific invocation.
        /// </summary>
        public CancellationToken CancellationToken { get; }

        /// <summary>
        /// Gets the <see cref="ILogger"/> instance assigned to this operation 
        /// for diagnostic and error reporting.
        /// </summary>
        public ILogger Logger { get; }

        /// <summary>
        /// Gets a value indicating whether the operation has completed successfully 
        /// without unhandled exceptions.
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// Gets the protocol-specific status code (e.g., 200, 404, 500) 
        /// resulting from the operation.
        /// </summary>
        public int StatusCode { get; }

        /// <summary>
        /// Gets the <see cref="Exception"/> that caused the operation to fail, 
        /// if any occurred during execution.
        /// </summary>
        public Exception? Exception { get; }

        /// <summary>
        /// Gets the <see cref="IAuthenticationManager"/> responsible for 
        /// the security context of this specific call.
        /// </summary>
        public IAuthenticationManager AuthenticationManager { get; }

        /// <summary>
        /// Gets a value indicating whether the operation resulted in an error 
        /// state or threw an exception.
        /// </summary>
        public bool HasError { get; }

        /// <summary>
        /// Transitions the context into a failure state by associating it 
        /// with the specified <see cref="Exception"/>.
        /// </summary>
        /// <param name="ex">The exception that occurred during the invocation.</param>
        public void SetException(Exception ex);
    }
}