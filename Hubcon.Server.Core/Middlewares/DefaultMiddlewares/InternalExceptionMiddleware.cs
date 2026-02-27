using Hubcon.Server.Abstractions.Delegates;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Interfaces;

using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Text;

namespace Hubcon.Server.Core.Middlewares.DefaultMiddlewares
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class InternalExceptionMiddleware(IInternalServerOptions options, ILogger<InternalExceptionMiddleware> logger) : IInternalExceptionMiddleware
    {
        Exception? exception = null;

        public async Task Execute(IOperationRequest request, IOperationContext context, PipelineDelegate next)
        {
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
            catch (Exception ex) when (RecordDiagnostics(ex))
            {
                exception = ex;
            }
            finally
            {
                bool? isError = null;
                StringBuilder? logMsg = null;
                StringBuilder? responseMsg = null;

                if (context.Response != null && !context.Response.Success)
                {
                    isError ??= true;

                    logMsg ??= new StringBuilder();

                    if (!string.IsNullOrWhiteSpace(context.Response.Error))
                        logMsg.AppendLine(context.Response.Error);

                    responseMsg ??= new StringBuilder();

                    if (!string.IsNullOrWhiteSpace(context.Response.Error))
                        responseMsg.AppendLine(context.Response.Error);
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

                    HubconResponse result = (context.Response as HubconResponse)! ?? HubconResponse.InternalError();

                    result.Error = options.DetailedErrorsEnabled ? createdResponseMsg : result.Error;
                    context.Response = result;
                    logger?.LogError("{createdLogMessage}\n{request}\n{result}", createdLogMessage, request, result);
                }
            }

            bool RecordDiagnostics(Exception ex)
            {
                exception = ex;
                return false;
            }
        }
    }
}