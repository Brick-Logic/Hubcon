using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Core.Configuration;
using Hubcon.Shared.Abstractions.Standard.Interfaces;
using Microsoft.AspNetCore.Builder;

namespace Hubcon.Server.Injection
{
    public class DefaultServerOptions : IServerOptions
    {
        public DefaultServerOptions(WebApplicationBuilder builder, ServerBuilder hubconBuilder)
        {
            Builder = builder;
            HubconServerBuilder = hubconBuilder;
        }

        public WebApplicationBuilder Builder { get; }
        public ServerBuilder HubconServerBuilder { get; }

        public void ConfigureCore(Action<ICoreServerOptions> coreServerOptions)
        {
            HubconServerBuilder.ConfigureCore(coreServerOptions);
        }

        public void AddGlobalMiddleware<T>()
        {
            HubconServerBuilder.AddGlobalMiddleware<T>();
        }

        public void AddGlobalMiddleware(Type middlewareType)
        {
            HubconServerBuilder.AddGlobalMiddleware(middlewareType);
        }

        public void AddController<T>(Action<IControllerOptions>? options = null) where T : class, IControllerContract
        {
            HubconServerBuilder.AddHubconController<T>(Builder, options);
        }

        public void AddController(Type controllerType, Action<IControllerOptions>? options = null)
        {
            HubconServerBuilder.AddHubconController(Builder, controllerType, options);
        }
    }
}
