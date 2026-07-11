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

        public IControllerOptions AddMiddleware<T>(LifeCycle cycle = LifeCycle.Scoped) where T : class, IMiddleware
        {
            return AddMiddleware(typeof(T), cycle);
        }

        public IControllerOptions AddMiddleware(Type middlewareType, LifeCycle cycle = LifeCycle.Scoped)
        {
            _builder.AddMiddleware(middlewareType);

            Action<IServiceCollection>? action = cycle switch
            {
                LifeCycle.Scoped => x => x.AddScoped(middlewareType),
                LifeCycle.Singleton => x => x.AddSingleton(middlewareType),
                LifeCycle.Transient => x => x.AddTransient(middlewareType),
                _ => null
            };

            ServicesToInject.Add(action!);
            return this;
        }
        
        public IControllerOptions AddMiddleware<T>(IRegisterer registerer) where T : class, IMiddleware
        {
            return AddMiddleware(typeof(T), registerer);
        }

        public IControllerOptions AddMiddleware(Type middlewareType, IRegisterer registerer)
        {
            _builder.AddMiddleware(middlewareType);
            Action<IServiceCollection>? action = x => registerer.Register(x);
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