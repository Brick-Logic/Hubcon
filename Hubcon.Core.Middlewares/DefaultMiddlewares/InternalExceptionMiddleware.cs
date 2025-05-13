using Hubcon.Core.Abstractions.Delegates;
using Hubcon.Core.Abstractions.Interfaces;
using Hubcon.Core.Invocation;

namespace Hubcon.Core.Middlewares.DefaultMiddlewares
{
    public class InternalExceptionMiddleware : IInternalExceptionMiddleware
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
                Console.WriteLine(ex.ToString());
                return;
            }
        }
    }
}
