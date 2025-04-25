using Hubcon.Core.Models.Middleware;

namespace Hubcon.Core.Models.Pipeline.Interfaces
{
    public interface IMiddlewareOptions
    {
        public IMiddlewareOptions AddMiddleware<T>() where T : class, IMiddleware;
        public IMiddlewareOptions AddMiddleware(Type middlewareType);
    }
}
