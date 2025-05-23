using Hubcon.Shared.Abstractions.Standard.Interfaces;
using Hubcon.Server.Abstractions.Interfaces;

namespace Hubcon.Server.Models
{
    public interface IControllerOptions
    {
        public void AddGlobalMiddleware<T>();
        public void AddGlobalMiddleware(Type middlewareType);
        public void AddController<T>(Action<IMiddlewareOptions>? options = null) where T : class, IControllerContract;
        public void AddController(Type controllerType, Action<IMiddlewareOptions>? options = null);
    }
}