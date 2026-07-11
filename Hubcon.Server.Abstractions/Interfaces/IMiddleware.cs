using Hubcon.Server.Abstractions.Delegates;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Interfaces;

namespace Hubcon
{
    /// <summary>
    /// Base marker interface for all middleware components in the Hubcon pipeline.
    /// </summary>
    [HubconPreserve]
    public interface IMiddleware
    {
    }

    /// <summary>
    /// Defines a middleware capable of participating in the standard request execution pipeline.
    /// </summary>
    public interface IExecutableMiddleware : IMiddleware
    {
        /// <summary>
        /// Executes the middleware logic for the current operation.
        /// </summary>
        /// <param name="request">The incoming <see cref="IOperationRequest"/>.</param>
        /// <param name="context">The execution <see cref="IOperationContext"/> for the current request.</param>
        /// <param name="next">The delegate representing the next middleware in the pipeline.</param>
        /// <returns>A <see cref="Task"/> that represents the asynchronous execution.</returns>
        public Task Execute(IOperationRequest request, IOperationContext context, PipelineDelegate next);
    }

    /// <summary>
    /// Defines middleware specialized in handling exceptions thrown during the pipeline execution.
    /// </summary>
    public interface IExceptionMiddleware : IExecutableMiddleware
    {
    }

    /// <summary>
    /// Defines middleware responsible for capturing and reporting telemetry data.
    /// </summary>
    public interface ITelemetryMiddleware : IExecutableMiddleware
    {
    }

    /// <summary>
    /// Defines middleware used for handling internal framework-level exceptions.
    /// </summary>
    public interface IInternalExceptionMiddleware : IExecutableMiddleware
    {
    }

    /// <summary>
    /// Defines middleware responsible for verifying the identity and permissions of the requester.
    /// </summary>
    public interface IAuthenticationMiddleware : IExecutableMiddleware
    {
    }

    /// <summary>
    /// Defines middleware focused on logging request and response data.
    /// </summary>
    public interface ILoggingMiddleware : IExecutableMiddleware
    {
    }

    /// <summary>
    /// Defines middleware that executes at the very beginning of the pipeline, before any significant processing occurs.
    /// </summary>
    public interface IPreRequestMiddleware : IExecutableMiddleware
    {
    }

    /// <summary>
    /// Defines a specialized middleware responsible for internal routing logic. 
    /// </summary>
    public interface IInternalRoutingMiddleware : IExecutableMiddleware
    {
    }

    /// <summary>
    /// Defines middleware that executes after the primary operation has been processed but before the response is finalized.
    /// </summary>
    public interface IPostRequestMiddleware : IExecutableMiddleware
    {
    }

    /// <summary>
    /// Defines middleware responsible for transforming or finalizing the outgoing response.
    /// </summary>
    public interface IResponseMiddleware : IExecutableMiddleware
    {
    }
}
