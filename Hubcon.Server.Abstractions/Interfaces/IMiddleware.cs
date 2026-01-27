using Hubcon.Server.Abstractions.Delegates;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Interfaces;

namespace Hubcon
{
    public interface IMiddleware
    {
    }

    public interface IExecutableMiddleware : IMiddleware
    {
        public Task Execute(IOperationRequest request, IOperationContext context, PipelineDelegate next);
    }

    public interface IExceptionMiddleware : IExecutableMiddleware
    {
    }

    public interface ITelemetryMiddleware : IExecutableMiddleware
    {
    }

    public interface IInternalExceptionMiddleware : IExecutableMiddleware
    {
    }

    public interface IAuthenticationMiddleware : IExecutableMiddleware
    {
    }

    public interface ILoggingMiddleware : IExecutableMiddleware
    {
    }

    public interface IPreRequestMiddleware : IExecutableMiddleware
    {
    }

    public interface IInternalRoutingMiddleware : IMiddleware
    {
        public Task Execute(IOperationRequest request, IOperationContext context, ResultHandlerDelegate resultHandler, PipelineDelegate next);
    }

    public interface IPostRequestMiddleware : IExecutableMiddleware
    {
    }

    public interface IResponseMiddleware : IExecutableMiddleware
    {
    }
}
