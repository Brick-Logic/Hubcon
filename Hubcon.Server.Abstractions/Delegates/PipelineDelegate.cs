using Hubcon.Server.Abstractions.Interfaces;

namespace Hubcon.Server.Abstractions.Delegates
{
    public delegate Task<IOperationContext> PipelineExecutionDelegate();
    public delegate Task PipelineDelegate();
}