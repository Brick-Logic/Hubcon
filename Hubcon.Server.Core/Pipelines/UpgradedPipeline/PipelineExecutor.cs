using Hubcon.Server.Abstractions.Delegates;
using Hubcon.Server.Abstractions.Interfaces;

namespace Hubcon.Server.Core.Pipelines.UpgradedPipeline
{
    internal class PipelineExecutor(PipelineExecutionDelegate pipelineReference) : IPipelineExecutor
    {
        public Task<IOperationContext> Execute()
        {
            return pipelineReference();
        }      
    }
}
