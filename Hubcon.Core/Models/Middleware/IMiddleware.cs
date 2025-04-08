using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Core.Models.Middleware
{
    public interface IMiddleware
    {
        public Task<MethodResponse?> Execute(MethodInvokeRequest request, Func<Task<MethodResponse?>> next);
    }

    public interface IExceptionMiddleware : IMiddleware
    {

    }

    public interface IAuthenticationMiddleware : IMiddleware
    {

    }

    public interface ILoggingMiddleware : IMiddleware
    {
        public Task Execute(MethodInvokeRequest request, MethodResponse response, Func<Task> next);
    }

    public interface IPreRequestMiddleware : IMiddleware
    {
    }

    public interface IPostRequestMiddleware : IMiddleware
    {
        public Task<MethodResponse?> Execute(MethodInvokeRequest request, MethodResponse response);
    }

    public interface IResponseMiddleware : IMiddleware
    {
        public Task<MethodResponse?> Execute(MethodResponse response);
    }
}
