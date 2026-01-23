using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Enums;
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

        public IControllerOptions AddMiddleware<T>(MiddlewareLifeCycle cycle = MiddlewareLifeCycle.Scoped) where T : class, IMiddleware
        {
            return AddMiddleware(typeof(T), cycle);
        }

        public IControllerOptions AddMiddleware(Type middlewareType, MiddlewareLifeCycle cycle = MiddlewareLifeCycle.Scoped)
        {
            _builder.AddMiddleware(middlewareType);

            Action<IServiceCollection>? action = cycle switch
            {
                MiddlewareLifeCycle.Scoped => x => x.AddScoped(middlewareType),
                MiddlewareLifeCycle.Singleton => x => x.AddSingleton(middlewareType),
                MiddlewareLifeCycle.Transient => x => x.AddTransient(middlewareType),
                _ => null
            };

            ServicesToInject.Add(action!);
            return this;
        }

        public IControllerOptions UseGlobalMiddlewaresFirst(bool value = true)
        {
            _builder.UseGlobalMiddlewaresFirst(value);
            return this;
        }
    }
}