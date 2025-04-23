using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Core.Models.Middleware
{
    public interface IMiddleware
    {
        public Task<IMethodResponse?> Execute(MethodInvokeRequest request, Func<Task<IMethodResponse?>> next);
    }

    public interface IExceptionMiddleware : IMiddleware
    {

    }

    public interface IAuthenticationMiddleware : IMiddleware
    {

    }

    public interface ILoggingMiddleware : IMiddleware
    {
        public Task Execute(MethodInvokeRequest request, IMethodResponse response, Func<Task> next);
    }

    public interface IPreRequestMiddleware : IMiddleware
    {
    }

    public interface IPostRequestMiddleware : IMiddleware
    {
        public Task<IMethodResponse?> Execute(MethodInvokeRequest request, IMethodResponse response);
    }

    public interface IResponseMiddleware : IMiddleware
    {
        public Task<IMethodResponse?> Execute(IMethodResponse response);
    }
}
