using Hubcon.Core.Abstractions.Delegates;

namespace Hubcon.Core.Abstractions.Interfaces
{
    public interface IMiddleware
    {
        public Task<IObjectMethodResponse?> Execute(IMethodInvokeRequest request, InvocationDelegate next);
    }

    public interface IExceptionMiddleware : IMiddleware
    {

    }

    public interface IAuthenticationMiddleware : IMiddleware
    {

    }

    public interface ILoggingMiddleware : IMiddleware
    {
        public Task Execute(IMethodInvokeRequest request, IObjectMethodResponse response, Func<Task> next);
    }

    public interface IPreRequestMiddleware : IMiddleware
    {
    }

    public interface IPostRequestMiddleware : IMiddleware
    {
        public Task<IObjectMethodResponse?> Execute(IMethodInvokeRequest request, IObjectMethodResponse response);
    }

    public interface IResponseMiddleware : IMiddleware
    {
        public Task<IObjectMethodResponse?> Execute(IObjectMethodResponse response);
    }
}
