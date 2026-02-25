using Hubcon;
using Microsoft.Extensions.Logging;

namespace ExampleMicroservices.Shared.Middlewares
{
    public class ExceptionMiddleware(ILogger<ExceptionMiddleware> logger) : IExceptionMiddleware
    {
        public async Task Execute(IOperationRequest request, IOperationContext context, PipelineDelegate next)
        {
            try
            {     
                await next();
            }
            catch (Exception ex)
            {
                context.Response = HubconResponse.InternalError();
                context.Exception = ex;
                logger.LogInformation(ex.ToString());
                return;
            }
        }
    }
}
