namespace Hubcon.Core.Abstractions.Interfaces
{
    public interface IPipelineExecutor
    {
        Task<IOperationContext> Execute();
    }
}