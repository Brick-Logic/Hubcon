namespace Hubcon.Server.Abstractions.Interfaces
{
    public interface IMiddlewareOptions
    {
        public IMiddlewareOptions AddMiddleware<T>() where T : class, IMiddleware;
        public IMiddlewareOptions AddMiddleware(Type middlewareType);
        public IMiddlewareOptions UseGlobalMiddlewaresFirst(bool? value = null);
    }
}
