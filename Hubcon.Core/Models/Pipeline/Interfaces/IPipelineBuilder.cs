using Autofac;
using Hubcon.Core.Middleware;
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
        public IPipeline Build(MethodInvokeRequest request, InvocationDelegate handler, ILifetimeScope serviceProvider);
    }
}
