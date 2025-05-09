using Autofac;
using Hubcon.Core.Abstractions.Delegates;

namespace Hubcon.Core.Abstractions.Interfaces
{
    public interface IPipelineBuilder
    {
        public IPipelineBuilder AddMiddleware<T>() where T : IMiddleware;
        public IPipelineBuilder AddMiddleware(Type middlewareType);
        public IPipeline Build(IMethodInvokeRequest request, InvocationDelegate handler, ILifetimeScope serviceProvider);
    }
}
