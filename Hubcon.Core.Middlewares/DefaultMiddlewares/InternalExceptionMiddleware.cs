using Hubcon.Core.Abstractions.Delegates;
using Hubcon.Core.Abstractions.Interfaces;
using Hubcon.Core.Invocation;
using Microsoft.Extensions.Logging;

namespace Hubcon.Core.Middlewares.DefaultMiddlewares
{
    public class InternalExceptionMiddleware(ILogger<InternalExceptionMiddleware> logger) : IInternalExceptionMiddleware
    {
        public async Task Execute(IOperationRequest request, IOperationContext context, PipelineDelegate next)
        {
            try
            {
                await next();
            }
            catch(Exception ex)
            {
                context.Result = new BaseOperationResponse(false, null, ex.Message);
                context.Exception = ex;
                logger.LogInformation(ex.ToString());
                return;
            }
        }
    }
}
