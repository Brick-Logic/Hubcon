using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Abstractions.Models;

namespace Hubcon
{
    /// <summary>
    /// This is the entry point for starting the asynchronous execution of the middleware chain.
    /// </summary>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation, containing the <see cref="IOperationContext"/> after execution.</returns>
    public delegate Task<IOperationContext> PipelineExecutionDelegate(PipelineState state);

    /// <summary>
    /// Represents a reference to the next middleware or action in the execution pipeline.
    /// This delegate is invoked by a middleware to pass control to the next component in the chain.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous completion of the next stage in the pipeline.</returns>
    public delegate Task PipelineDelegate();
}