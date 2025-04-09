using Hubcon.Core.Models.Pipeline.Interfaces;

namespace Hubcon.Core.Models.Pipeline
{
    internal class Pipeline : IPipeline
    {
        public Pipeline(Type controllerType, Func<Task<MethodResponse?>> pipelineReference)
        {
            pipelineMethod = pipelineReference!;
        }

        private Func<Task<MethodResponse>> pipelineMethod { get; }

        public async Task<MethodResponse> Execute()
        {
            return await pipelineMethod();
        }
    }
}
