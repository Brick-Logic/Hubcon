using Autofac;
using Hubcon.Core.Models;
using Hubcon.Core.Models.Middleware;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;

namespace Hubcon.Core.Middleware
{
    public class LoggingMiddleware : ILoggingMiddleware
    {
        private Stopwatch sw;

        public LoggingMiddleware()
        {
            sw = Stopwatch.StartNew();
        }

        public async Task<IMethodResponse?> Execute(MethodInvokeRequest request, Func<Task<IMethodResponse?>> next)
        {
            sw.Restart();

            Console.WriteLine($"[Inicio] Methodo {request.MethodName} llamado...");
            Stopwatch stopwatch = Stopwatch.StartNew();

            var result = await next();

            stopwatch.Stop();
            var nanosecs = (double)stopwatch.ElapsedTicks / Stopwatch.Frequency * 1_000;
            Console.WriteLine($"[Fin] Methodo {request.MethodName} finalizado. Tiempo: {nanosecs} ms.");

            return result;
        }

        public Task Execute(MethodInvokeRequest request, IMethodResponse response, Func<Task> next)
        {
            sw.Stop();
            var nanosecs = (double)sw.ElapsedTicks / Stopwatch.Frequency * 1_000;
            Console.WriteLine($"[Retorno] Tiempo: {nanosecs} ms.");
            return next();
        }
    }
}
