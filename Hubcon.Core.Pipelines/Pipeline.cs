using Hubcon.Core.Abstractions.Interfaces;

namespace Hubcon.Core.Pipelines
{
    internal class Pipeline : IPipeline
    {
        public Pipeline(Func<Task<IObjectMethodResponse>> pipelineReference)
        {
            pipelineMethod = pipelineReference!;
        }

        private Func<Task<IObjectMethodResponse>> pipelineMethod { get; }

        public async Task<IObjectMethodResponse> Execute()
        {
            return await pipelineMethod();
        }
    }
}
