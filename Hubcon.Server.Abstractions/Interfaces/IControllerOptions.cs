using Hubcon.Shared.Abstractions.Standard.Interfaces;

namespace Hubcon.Server.Abstractions.Interfaces
{
    public interface IControllerOptions
    {
        public void AddGlobalMiddleware<T>();
        public void AddGlobalMiddleware(Type middlewareType);
        public void AddController<T>(Action<IMiddlewareOptions>? options = null) where T : class, IControllerContract;
        public void AddController(Type controllerType, Action<IMiddlewareOptions>? options = null);
    }
}