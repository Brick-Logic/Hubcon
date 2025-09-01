using Autofac;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Core.Extensions;
using Hubcon.Shared.Core.Extensions;

namespace Hubcon.Server.Core.Middlewares
{
    internal sealed class ControllerOptions : IControllerOptions
    {
        IPipelineBuilder _builder;
        public List<Action<ContainerBuilder>> ServicesToInject;

        public ControllerOptions(IPipelineBuilder builder, List<Action<ContainerBuilder>> servicesToInject)
        {
            _builder = builder;
            ServicesToInject = servicesToInject;
        }

        public IControllerOptions AddMiddleware<T>() where T : class, IMiddleware
        {
            return AddMiddleware(typeof(T));
        }

        public IControllerOptions AddMiddleware(Type middlewareType)
        {
            _builder.AddMiddleware(middlewareType);
            ServicesToInject.Add(x => x.RegisterType(middlewareType));
            return this;
        }

        public IControllerOptions UseGlobalMiddlewaresFirst(bool value = true)
        {
            _builder.UseGlobalMiddlewaresFirst(value);
            return this;
        }
    }
}
