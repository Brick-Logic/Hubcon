using Hubcon.Core.Abstractions.Delegates;

namespace Hubcon.Core.Abstractions.Interfaces
{
    public interface IMiddlewareProvider
    {
        public IPipeline GetPipeline(IOperationBlueprint descriptor, IOperationRequest request, IServiceProvider serviceProvider, PipelineDelegate handler);
    }
}
