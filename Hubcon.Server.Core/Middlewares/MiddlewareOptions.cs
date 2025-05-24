using Autofac;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Core.Extensions;
using Hubcon.Shared.Core.Extensions;

namespace Hubcon.Server.Core.Middlewares
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
