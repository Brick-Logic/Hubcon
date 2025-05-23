using Hubcon.Server.Abstractions.Delegates;
using Hubcon.Server.Abstractions.Interfaces;

namespace Hubcon.Server.Core.Pipelines.UpgradedPipeline
{
    internal class PipelineExecutor : IPipelineExecutor
    {
        private PipelineDelegate Pipeline { get; }
        public IOperationContext Context { get; }

        public PipelineExecutor(PipelineDelegate pipelineReference, IOperationContext context)
        {
            Pipeline = pipelineReference!;
            Context = context;
        }

        public async Task<IOperationContext> Execute()
        {
            await Pipeline();
            return Context;
        }      
    }
}
