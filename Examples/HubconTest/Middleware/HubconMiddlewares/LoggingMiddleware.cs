using Hubcon.Core.Models;
using Hubcon.Core.Models.Middleware;
using System.Diagnostics;

namespace HubconTest.Middleware.HubconMiddlewares
{
    public class LoggingMiddleware : ILoggingMiddleware
    {
        public static Stopwatch stopwatch = new Stopwatch();

        public LoggingMiddleware(IServiceProvider scope, IConfiguration configuration)
        {
            var test = scope;          
        }

        public Task<MethodResponse?> Execute(MethodInvokeRequest request, Func<Task<MethodResponse?>> next)
        {
            Console.WriteLine($"[Inicio] Methodo {request.MethodName} llamado...");
            stopwatch.Reset();
            stopwatch.Start();
            return next();
        }

        public Task Execute(MethodInvokeRequest request, MethodResponse response, Func<Task> next)
        {
            stopwatch.Stop();
            var nanosecs = (double)stopwatch.ElapsedTicks / Stopwatch.Frequency * 1_000;
            Console.WriteLine($"[Fin] Methodo {request.MethodName} finalizado. Tiempo: {nanosecs} ms.");
            return next();
        }
    }
}
