using Hubcon.Server.Core.Configuration;
using Hubcon.Shared.Abstractions.Standard.Interfaces;

namespace Hubcon.Server.Abstractions.Interfaces
{
    public interface IServerOptions
    {
        public void AddGlobalMiddleware<T>();
        public void AddGlobalMiddleware(Type middlewareType);
        public void AddController<T>(Action<IControllerOptions>? options = null) where T : class, IControllerContract;
        public void AddController(Type controllerType, Action<IControllerOptions>? options = null);
        public void ConfigureCore(Action<ICoreServerOptions> coreServerOptions);
        public void AddAuthentication();
        public void AutoRegisterControllers();
    }
}