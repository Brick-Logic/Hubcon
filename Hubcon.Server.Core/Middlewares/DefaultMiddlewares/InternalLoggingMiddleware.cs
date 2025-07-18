using Hubcon.Server.Abstractions.Delegates;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Interfaces;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Hubcon.Server.Core.Middlewares.DefaultMiddlewares
{
    internal sealed class InternalLoggingMiddleware(ILogger<InternalLoggingMiddleware> logger) : ILoggingMiddleware
    {
        public async Task Execute(IOperationRequest request, IOperationContext context, PipelineDelegate next)
        {
            logger.LogInformation($"[Inicio] Methodo {request.OperationName} llamado...");
            Stopwatch stopwatch = Stopwatch.StartNew();

            try 
            {        
                await next();
            }
            finally
            {
                stopwatch.Stop();
                var milisecs = (double)stopwatch.ElapsedTicks / Stopwatch.Frequency * 1_000;
                logger.LogInformation($"[Fin] Methodo {request.OperationName} finalizado. Tiempo: {milisecs} ms.");
            }
        }
    }
}
