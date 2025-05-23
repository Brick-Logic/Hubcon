namespace Hubcon.Server.Abstractions.Interfaces
{
    public interface IPipelineExecutor
    {
        Task<IOperationContext> Execute();
    }
}