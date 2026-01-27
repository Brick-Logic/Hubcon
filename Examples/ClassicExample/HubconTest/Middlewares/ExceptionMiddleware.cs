using Hubcon;

namespace HubconTest.Middlewares
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
                context.Response =  HubconResponse.InternalError(ex, ex.Message);
                context.Exception = ex;
                logger.LogInformation(ex.ToString());
                return;
            }
        }
    }
}
