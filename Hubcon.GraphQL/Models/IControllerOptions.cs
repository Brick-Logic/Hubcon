using Hubcon.Core.Models.Interfaces;
using Hubcon.Core.Models.Pipeline.Interfaces;

namespace Hubcon.GraphQL.Models
{
    public interface IControllerOptions
    {
        public void AddGlobalMiddleware<T>();
        public void AddGlobalMiddleware(Type middlewareType);
        public void AddController<T>(Action<IMiddlewareOptions>? options = null) where T : class, IHubconControllerContract;
        public void AddController(Type controllerType, Action<IMiddlewareOptions>? options = null);
    }
}