using Hubcon.Server.Abstractions.Delegates;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;

namespace BlazorTestServer.Middlewares
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
				context.Result = new BaseOperationResponse<object>(false, default!, ex.Message);
				context.Exception = ex;
                logger.LogInformation(ex.ToString());
				return;
			}
        }
    }
}
