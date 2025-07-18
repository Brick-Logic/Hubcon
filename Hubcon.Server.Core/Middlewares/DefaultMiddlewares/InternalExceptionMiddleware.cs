using Hubcon.Server.Abstractions.Delegates;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Core.Configuration;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Microsoft.Extensions.Logging;
using System.ComponentModel;

namespace Hubcon.Server.Core.Middlewares.DefaultMiddlewares
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class InternalExceptionMiddleware(IInternalServerOptions options, ILogger<InternalExceptionMiddleware> logger) : IInternalExceptionMiddleware
    {
        public async Task Execute(IOperationRequest request, IOperationContext context, PipelineDelegate next)
        {
            try
            {
                await next();

                if(context.Exception is not null)
                {
                    if (options.DetailedErrorsEnabled)
                    {
                        context.Result = new BaseOperationResponse<object>(false, null!, context.Exception.ToString());
                        logger.LogInformation(context.Exception.ToString());
                    }
                    else
                    {
                        context.Result = new BaseOperationResponse<object>(false, null!, context.Exception.Message);

                        logger.LogInformation(context.Exception.ToString());
                    }
                    return;
                }
            }
            catch(Exception ex)
            {
                if (options.DetailedErrorsEnabled)
                {
                    context.Result = new BaseOperationResponse<object>(false, null!, ex.ToString());
                    logger.LogInformation(ex.ToString());
                }
                else
                {
                    context.Result = new BaseOperationResponse<object>(false, null!, ex.Message);
                    logger.LogInformation(ex.ToString());
                }
                return;
            }
        }
    }
}
