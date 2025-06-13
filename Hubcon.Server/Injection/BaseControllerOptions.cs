using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Standard.Interfaces;
using Microsoft.AspNetCore.Builder;

namespace Hubcon.Server.Injection
{
    public class BaseControllerOptions : IControllerOptions
    {
        public BaseControllerOptions(WebApplicationBuilder builder, HubconServerBuilder hubconBuilder)
        {
            Builder = builder;
            HubconServerBuilder = hubconBuilder;
        }

        public WebApplicationBuilder Builder { get; }
        public HubconServerBuilder HubconServerBuilder { get; }

        public void AddGlobalMiddleware<T>()
        {
            HubconServerBuilder.Current.AddGlobalMiddleware<T>();
        }

        public void AddGlobalMiddleware(Type middlewareType)
        {
            HubconServerBuilder.Current.AddGlobalMiddleware(middlewareType);
        }

        public void AddController<T>(Action<IMiddlewareOptions>? options = null) where T : class, IControllerContract
        {
            HubconServerBuilder.Current.AddHubconController<T>(Builder, options);
        }

        public void AddController(Type controllerType, Action<IMiddlewareOptions>? options = null)
        {
            HubconServerBuilder.Current.AddHubconController(Builder, controllerType, options);
        }
    }
}
