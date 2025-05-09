using Hubcon.Core.Abstractions.Interfaces;
using Hubcon.Core.Abstractions.Standard.Interfaces;

namespace Hubcon.GraphQL.Models
{
    public interface IControllerOptions
    {
        public void AddGlobalMiddleware<T>();
        public void AddGlobalMiddleware(Type middlewareType);
        public void AddController<T>(Action<IMiddlewareOptions>? options = null) where T : class, IControllerContract;
        public void AddController(Type controllerType, Action<IMiddlewareOptions>? options = null);
    }
}