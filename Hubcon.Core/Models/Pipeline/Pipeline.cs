using Hubcon.Core.Models.Pipeline.Interfaces;

namespace Hubcon.Core.Models.Pipeline
{
    internal class Pipeline : IPipeline
    {
        public Pipeline(Func<Task<IMethodResponse>> pipelineReference)
        {
            pipelineMethod = pipelineReference!;
        }

        private Func<Task<IMethodResponse>> pipelineMethod { get; }

        public async Task<IMethodResponse> Execute()
        {
            return await pipelineMethod();
        }
    }
}
