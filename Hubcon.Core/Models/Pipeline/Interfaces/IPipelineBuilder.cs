using Autofac;
using Hubcon.Core.Models.Middleware;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Core.Models.Pipeline.Interfaces
{
    public interface IPipelineBuilder
    {
        public IPipelineBuilder AddMiddleware<T>() where T : IMiddleware;
        public IPipelineBuilder AddMiddleware(Type middlewareType);
        public IPipeline Build(MethodInvokeRequest request, Func<Task<IMethodResponse?>> handler, ILifetimeScope serviceProvider);
    }
}
