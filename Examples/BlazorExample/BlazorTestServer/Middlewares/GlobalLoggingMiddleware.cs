using Hubcon.Server.Abstractions.Delegates;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Interfaces;
using Microsoft.Extensions.Logging;

namespace BlazorTestServer.Middlewares
{
    public class GlobalLoggingMiddleware(ILogger<GlobalLoggingMiddleware> logger) : ILoggingMiddleware
    {
        public async Task Execute(IOperationRequest request, IOperationContext context, PipelineDelegate next)
        {
            try
            {
                logger.LogInformation($"[Global] Operacion {request.OperationName} iniciada.");
                await next();
            }
            finally
            {
                logger.LogInformation($"[Global] Operacion {request.OperationName} terminada.");
            }
        }
    }
}
