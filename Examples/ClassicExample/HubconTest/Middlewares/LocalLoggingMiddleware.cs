
using System.Diagnostics.CodeAnalysis;
using Hubcon;
using IMiddleware = Microsoft.AspNetCore.Http.IMiddleware;

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
