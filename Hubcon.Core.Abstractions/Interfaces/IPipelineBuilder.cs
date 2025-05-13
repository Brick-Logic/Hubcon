using Autofac;
using Hubcon.Core.Abstractions.Delegates;

namespace Hubcon.Core.Abstractions.Interfaces
{
    public interface IPipelineBuilder
    {
        public IPipelineBuilder AddMiddleware<T>() where T : IMiddleware;
        public IPipelineBuilder AddMiddleware(Type middlewareType);
        public IPipelineExecutor Build(IOperationRequest request, IOperationContext context, ResultHandlerDelegate resultHandler, IServiceProvider serviceProvider);
        public void UseGlobalMiddlewaresFirst(bool? value = null);
    }
}
