using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Core.Configuration;
using Hubcon.Server.Core.Middlewares.DefaultMiddlewares;
using Hubcon.Shared.Abstractions.Standard.Interfaces;
using Microsoft.AspNetCore.Builder;

namespace Hubcon.Server.Injection
{
    //internal sealed class BaseServerOptions : IServerOptions
    //{
    //    public BaseServerOptions(WebApplicationBuilder builder, ServerBuilder serverBuilder)
    //    {
    //        Builder = builder;
    //        ServerBuilder = serverBuilder;
    //    }

    //    public WebApplicationBuilder Builder { get; }
    //    public ServerBuilder ServerBuilder { get; }

    //    public void ConfigureCore(Action<ICoreServerOptions> coreServerOptions)
    //    {
    //        ServerBuilder.Current.ConfigureCore(coreServerOptions);
    //    }

    //    public void AddGlobalMiddleware<T>()
    //    {
    //        ServerBuilder.Current.AddGlobalMiddleware<T>();
    //    }

    //    public void AddGlobalMiddleware(Type middlewareType)
    //    {
    //        ServerBuilder.Current.AddGlobalMiddleware(middlewareType);
    //    }

    //    public void AddController<T>(Action<IControllerOptions>? options = null) where T : class, IControllerContract
    //    {
    //        ServerBuilder.Current.AddHubconController<T>(Builder, options);
    //    }

    //    public void AddController(Type controllerType, Action<IControllerOptions>? options = null)
    //    {
    //        ServerBuilder.Current.AddHubconController(Builder, controllerType, options);
    //    }

    //    public void AddAuthentication()
    //    {
    //        ServerBuilder.Current.AddGlobalMiddleware<AuthenticationMiddleware>();
    //    }

    //    public void AutoRegisterControllers()
    //    {
    //        throw new NotImplementedException();
    //    }
    //}
}
