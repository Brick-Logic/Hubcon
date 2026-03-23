using Hubcon.Server.Abstractions.Delegates;
#pragma warning disable CS1591
using Hubcon.Shared.Abstractions.Interfaces;

namespace Hubcon.Server.Abstractions.Interfaces
{
    public interface IMiddlewareProvider
    {
        public IPipeline GetPipeline(IOperationBlueprint descriptor, IOperationRequest request, IServiceProvider serviceProvider, PipelineDelegate handler);
    }
}
