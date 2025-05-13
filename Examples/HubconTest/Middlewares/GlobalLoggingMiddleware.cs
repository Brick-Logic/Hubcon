using Hubcon.Core.Abstractions.Delegates;
using Hubcon.Core.Abstractions.Interfaces;

namespace HubconTest.Middlewares
{
    public class GlobalLoggingMiddleware : ILoggingMiddleware
    {
        public async Task Execute(IOperationRequest request, IOperationContext context, PipelineDelegate next)
        {
            try
            {
                Console.WriteLine($"[Global] Operacion {request.OperationName} iniciada.");
                await next();
            }
            finally
            {
                Console.WriteLine($"[Global] Operacion {request.OperationName} terminada.");
            }
        }
    }
}
