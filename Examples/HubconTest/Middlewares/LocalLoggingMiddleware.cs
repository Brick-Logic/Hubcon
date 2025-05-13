using Hubcon.Core.Abstractions.Delegates;
using Hubcon.Core.Abstractions.Interfaces;

namespace HubconTest.Middlewares
{
    public class LocalLoggingMiddleware : ILoggingMiddleware
    {
        public async Task Execute(IOperationRequest request, IOperationContext context, PipelineDelegate next)
        {
            try
            {
                Console.WriteLine($"[Local] Operacion {request.OperationName} iniciada.");
                await next();
            }
            finally
            {
                Console.WriteLine($"[Local] Operacion {request.OperationName} terminada.");
            }
        }
    }
}
