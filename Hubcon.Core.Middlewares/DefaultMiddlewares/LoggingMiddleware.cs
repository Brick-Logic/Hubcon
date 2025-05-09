using Autofac;
using Hubcon.Core.Abstractions.Delegates;
using Hubcon.Core.Abstractions.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;

namespace Hubcon.Core.Middlewares.DefaultMiddlewares
{
    public class LoggingMiddleware : ILoggingMiddleware
    {
        private Stopwatch sw;

        public LoggingMiddleware()
        {
            sw = new Stopwatch();
        }

        public async Task<IObjectMethodResponse?> Execute(IMethodInvokeRequest request, InvocationDelegate next)
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

        public Task Execute(IMethodInvokeRequest request, IObjectMethodResponse response, Func<Task> next)
        {
            sw.Stop();
            var nanosecs = (double)sw.ElapsedTicks / Stopwatch.Frequency * 1_000;
            Console.WriteLine($"[Retorno] Tiempo: {nanosecs} ms.");
            return next();
        }
    }
}
