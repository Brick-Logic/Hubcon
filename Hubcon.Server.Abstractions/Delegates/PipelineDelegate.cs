using Hubcon.Server.Abstractions.Interfaces;

namespace Hubcon
{
    public delegate Task<IOperationContext> PipelineExecutionDelegate();
    public delegate Task PipelineDelegate();
}