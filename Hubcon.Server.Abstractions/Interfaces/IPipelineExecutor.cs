#pragma warning disable CS1591

namespace Hubcon.Server.Abstractions.Interfaces
{
    public interface IPipelineExecutor
    {
        ValueTask<IOperationContext> Execute();
    }
}