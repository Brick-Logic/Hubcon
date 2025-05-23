using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Interfaces;

namespace Hubcon.Server.Core.Pipelines
{
    internal class Pipeline : IPipeline
    {
        public Pipeline(Func<Task<IObjectOperationResponse>> pipelineReference)
        {
            pipelineMethod = pipelineReference!;
        }

        private Func<Task<IObjectOperationResponse>> pipelineMethod { get; }

        public async Task<IObjectOperationResponse> Execute()
        {
            return await pipelineMethod();
        }
    }
}
