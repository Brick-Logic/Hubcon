using Hubcon.Core.Abstractions.Delegates;
using Hubcon.Core.Abstractions.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Core.Pipelines.UpgradedPipeline
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
