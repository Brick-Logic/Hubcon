using Hubcon.Core.Models;
using Hubcon.Core.Models.Middleware;

namespace HubconTest.Middleware.HubconMiddlewares
{
    public class LoggingMiddleware : ILoggingMiddleware
    {
        public Task<MethodResponse?> Execute(MethodInvokeRequest request, Func<Task<MethodResponse?>> next)
        {
            Console.WriteLine("Middleware funciona1");
            return next();
        }

        public Task Execute(MethodInvokeRequest request, MethodResponse response, Func<Task> next)
        {
            Console.WriteLine("Middleware funciona2");
            return next();
        }
    }
}
