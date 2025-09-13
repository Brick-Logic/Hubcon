using Hubcon.Server.Abstractions.Delegates;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Core.Configuration;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Text;

namespace Hubcon.Server.Core.Middlewares.DefaultMiddlewares
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class InternalExceptionMiddleware(IInternalServerOptions options, ILogger<InternalExceptionMiddleware> logger) : IInternalExceptionMiddleware
    {
        public async Task Execute(IOperationRequest request, IOperationContext context, PipelineDelegate next)
        {
            Exception? exception = null;

            try
            {
                await next();
            }
            catch (TaskCanceledException)
            {
                exception = new OperationCanceledException();
            }
            catch (OperationCanceledException)
            {
                exception = new OperationCanceledException();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
            finally
            {
                bool? isError = null;
                StringBuilder? logMsg = null;
                StringBuilder? responseMsg = null;

                if (context.Result != null && !context.Result.Success)
                {
                    isError ??= true;

                    logMsg ??= new StringBuilder();

                    if (!string.IsNullOrWhiteSpace(context.Result.Error))
                        logMsg.AppendLine(context.Result.Error);

                    responseMsg ??= new StringBuilder();

                    if (!string.IsNullOrWhiteSpace(context.Result.Error))
                        responseMsg.AppendLine(context.Result.Error);
                }

                if (context.Exception != null)
                {
                    isError ??= true;

                    if (!string.IsNullOrWhiteSpace(context.Exception.Message))
                    {
                        logMsg ??= new StringBuilder();
                        responseMsg ??= new StringBuilder();

                        logMsg.AppendLine(context.Exception.ToString());

                        if (options.DetailedErrorsEnabled)
                        {
                            if (!string.IsNullOrWhiteSpace(context.Exception.Message))
                                responseMsg.AppendLine(context.Exception.Message);

                            if (!string.IsNullOrWhiteSpace(context.Exception.StackTrace))
                                responseMsg.AppendLine(context.Exception.StackTrace);                         
                        }
                        else
                        {
                            responseMsg.AppendLine(context.Exception.Message);
                        }
                    }
                }

                if (exception != null)
                {
                    isError ??= true;

                    if (!string.IsNullOrWhiteSpace(exception.Message))
                    {
                        logMsg ??= new StringBuilder();
                        responseMsg ??= new StringBuilder();

                        logMsg.AppendLine(exception.ToString());

                        if (options.DetailedErrorsEnabled)
                        {
                            if (!string.IsNullOrWhiteSpace(exception.Message))
                                responseMsg.AppendLine(exception.Message);

                            if (!string.IsNullOrWhiteSpace(exception.StackTrace))
                                responseMsg.AppendLine(exception.StackTrace);                           
                        }
                        else
                        {
                            responseMsg.AppendLine(exception.Message);
                        }                  
                    }
                }

                if (isError == true)
                {
                    var createdLogMessage = logMsg!.ToString();
                    var createdResponseMsg = responseMsg!.ToString();

                    var response = new BaseOperationResponse<object>(
                        false,
                        null!,
                        options.DetailedErrorsEnabled ? createdResponseMsg : "Internal server error");

                    var result = context.Result;

                    logger?.LogError("{createdLogMessage}\n{request}\n{result}", createdLogMessage, request, result);

                    context.Result = response;
                }
            }
        }
    }
}