using Hubcon.Server.Abstractions.Delegates;
#pragma warning disable CS1591
using Hubcon.Shared.Abstractions.Interfaces;

namespace Hubcon.Server.Abstractions.Interfaces
{
    public interface IPipelineBuilder
    {
        public IPipelineBuilder AddMiddleware<T>() where T : IMiddleware;
        public IPipelineBuilder AddMiddleware(Type middlewareType);
        public IPipelineExecutor Build(IOperationRequest request, IOperationContext context, IServiceProvider serviceProvider);
        public void UseGlobalMiddlewaresFirst(bool? value = null);
    }
}