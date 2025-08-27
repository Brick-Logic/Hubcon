using Hubcon.Server.Abstractions.Delegates;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Interfaces;

namespace HubconTest.Middlewares
{
    public class LocalLoggingMiddleware(ILogger<LocalLoggingMiddleware> logger) : ILoggingMiddleware
    {
        public async Task Execute(IOperationRequest request, IOperationContext context, PipelineDelegate next)
        {
            try
            {
                logger.LogInformation($"[Operation] Operacion {request.OperationName} iniciada.");
                await next();
            }
            finally
            {
                logger.LogInformation($"[Operation] Operacion {request.OperationName} terminada.");
            }
        }
    }

    public class ClassLoggingMiddleware(ILogger<LocalLoggingMiddleware> logger) : ILoggingMiddleware
    {
        public async Task Execute(IOperationRequest request, IOperationContext context, PipelineDelegate next)
        {
            try
            {
                logger.LogInformation($"[Class] Operacion {request.OperationName} iniciada.");
                await next();
            }
            finally
            {
                logger.LogInformation($"[Class] Operacion {request.OperationName} terminada.");
            }
        }
    }
}
