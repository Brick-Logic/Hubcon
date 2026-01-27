using Hubcon;
using Hubcon.Server.Abstractions.Delegates;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Interfaces;

namespace BlazorTestServer.Middlewares
{
    public class LocalLoggingMiddleware(ILogger<LocalLoggingMiddleware> logger) : ILoggingMiddleware
    {
        public async Task Execute(IOperationRequest request, IOperationContext context, PipelineDelegate next)
        {
            try
            {
                logger.LogInformation($"[Local] Operacion {request.OperationName} iniciada.");
                await next();
            }
            finally
            {
                logger.LogInformation($"[Local] Operacion {request.OperationName} terminada.");
            }
        }
    }
}
