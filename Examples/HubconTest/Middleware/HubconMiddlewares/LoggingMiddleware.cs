using Autofac;
using Hubcon.Core.Models;
using Hubcon.Core.Models.Middleware;
using System.Diagnostics;

namespace HubconTest.Middleware.HubconMiddlewares
{
    public class LoggingMiddleware : ILoggingMiddleware
    {
        public LoggingMiddleware()
        {
        }

        public Task<MethodResponse?> Execute(MethodInvokeRequest request, Func<Task<MethodResponse?>> next)
        {
            Console.WriteLine($"[Inicio] Methodo {request.MethodName} llamado...");


            var stopwatch = Stopwatch.StartNew();
            var task = next();
            stopwatch.Stop();


            var nanosecs = (double)stopwatch.ElapsedTicks / Stopwatch.Frequency * 1_000;
            Console.WriteLine($"[Fin] Methodo {request.MethodName} finalizado. Tiempo: {nanosecs} ms.");

            return task;
        }

        public Task Execute(MethodInvokeRequest request, MethodResponse response, Func<Task> next)
        {
            return next();
        }
    }
}
