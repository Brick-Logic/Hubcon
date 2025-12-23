using Hubcon.Server.Abstractions.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Hubcon.Server.Core.Middlewares
{
    internal sealed class ControllerOptions : IControllerOptions
    {
        IPipelineBuilder _builder;
        public List<Action<IServiceCollection>> ServicesToInject;

        public ControllerOptions(IPipelineBuilder builder, List<Action<IServiceCollection>> servicesToInject)
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
            ServicesToInject.Add(x => x.AddScoped(middlewareType));
            return this;
        }

        public IControllerOptions UseGlobalMiddlewaresFirst(bool value = true)
        {
            _builder.UseGlobalMiddlewaresFirst(value);
            return this;
        }
    }
}
