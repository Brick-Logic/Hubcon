using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Models;
using Hubcon.Shared.Abstractions.Standard.Interfaces;
using Microsoft.AspNetCore.Builder;

namespace Hubcon.Server.Injection
{
    public class DefaultControllerOptions : IControllerOptions
    {
        public DefaultControllerOptions(WebApplicationBuilder builder, HubconServerBuilder hubconBuilder)
        {
            Builder = builder;
            HubconServerBuilder = hubconBuilder;
        }

        public WebApplicationBuilder Builder { get; }
        public HubconServerBuilder HubconServerBuilder { get; }

        public void AddGlobalMiddleware<T>()
        {
            HubconServerBuilder.AddGlobalMiddleware<T>();
        }

        public void AddGlobalMiddleware(Type middlewareType)
        {
            HubconServerBuilder.AddGlobalMiddleware(middlewareType);
        }

        public void AddController<T>(Action<IMiddlewareOptions>? options = null) where T : class, IControllerContract
        {
            HubconServerBuilder.AddHubconController<T>(Builder, options);
        }

        public void AddController(Type controllerType, Action<IMiddlewareOptions>? options = null)
        {
            HubconServerBuilder.AddHubconController(Builder, controllerType, options);
        }
    }
}
