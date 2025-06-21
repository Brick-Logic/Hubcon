namespace Hubcon.Server.Abstractions.Interfaces
{
    public interface IControllerOptions
    {
        public IControllerOptions AddMiddleware<T>() where T : class, IMiddleware;
        public IControllerOptions AddMiddleware(Type middlewareType);
        public IControllerOptions UseGlobalMiddlewaresFirst(bool value = true);
    }
}