using Hubcon.Shared.Abstractions.Enums;

namespace Hubcon.Server.Abstractions.Interfaces
{
    public interface IControllerOptions
    {
        public IControllerOptions AddMiddleware<T>(MiddlewareLifeCycle cycle = MiddlewareLifeCycle.Scoped) where T : class, IMiddleware;
        public IControllerOptions AddMiddleware(Type middlewareType, MiddlewareLifeCycle cycle = MiddlewareLifeCycle.Scoped);
        public IControllerOptions UseGlobalMiddlewaresFirst(bool value = true);
    }
}