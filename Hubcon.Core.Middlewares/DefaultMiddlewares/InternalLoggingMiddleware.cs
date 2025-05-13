using Autofac;
using Hubcon.Core.Abstractions.Delegates;
using Hubcon.Core.Abstractions.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;

namespace Hubcon.Core.Middlewares.DefaultMiddlewares
{
    public class InternalLoggingMiddleware : ILoggingMiddleware
    {
        private Stopwatch sw;

        public InternalLoggingMiddleware()
        {
            sw = new Stopwatch();
        }

        public async Task Execute(IOperationRequest request, IOperationContext context, PipelineDelegate next)
        {

            Console.WriteLine($"[Inicio] Methodo {request.OperationName} llamado...");
            Stopwatch stopwatch = Stopwatch.StartNew();

            try 
            {        
                await next();
            }
            finally
            {
                stopwatch.Stop();
                var milisecs = (double)stopwatch.ElapsedTicks / Stopwatch.Frequency * 1_000;
                Console.WriteLine($"[Fin] Methodo {request.OperationName} finalizado. Tiempo: {milisecs} ms.");
            }
        }
    }
}
