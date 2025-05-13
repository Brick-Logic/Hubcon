using Autofac;
using Hubcon.Core.Abstractions.Interfaces;
using Hubcon.Core.Extensions;

namespace Hubcon.Core.Middlewares
{
    public class MiddlewareOptions : IMiddlewareOptions
    {
        IPipelineBuilder _builder;
        public List<Action<ContainerBuilder>> ServicesToInject;

        public MiddlewareOptions(IPipelineBuilder builder, List<Action<ContainerBuilder>> servicesToInject)
        {
            _builder = builder;
            ServicesToInject = servicesToInject;
        }

        public IMiddlewareOptions AddMiddleware<T>() where T : class, IMiddleware
        {
            return AddMiddleware(typeof(T));
        }

        public IMiddlewareOptions AddMiddleware(Type middlewareType)
        {
            _builder.AddMiddleware(middlewareType);
            ServicesToInject.Add(x => x.RegisterWithInjector(y => y.RegisterType(middlewareType)));
            return this;
        }

        public IMiddlewareOptions UseGlobalMiddlewaresFirst(bool? value = null)
        {
            _builder.UseGlobalMiddlewaresFirst(value);
            return this;
        }
    }
}
