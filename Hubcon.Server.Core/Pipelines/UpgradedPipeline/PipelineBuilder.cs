using Hubcon.Server.Abstractions.Delegates;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Linq;

namespace Hubcon.Server.Core.Pipelines.UpgradedPipeline
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class PipelineBuilder : IPipelineBuilder
    {
        private static bool GlobalMiddlewaresFirst { get; set; } = false;

        private static Type GlobalInternalExceptionMiddleware { get; set; }
        private static Type GlobalExceptionMiddleware { get; set; }
        private static List<Type> GlobalLoggingMiddlewares { get; } = new();
        private static List<Type> GlobalAuthenticationMiddlewares { get; } = new();
        private static List<Type> GlobalAuthorizationMiddlewares { get; } = new();
        private static List<Type> GlobalPreRequestMiddlewares { get; } = new();
        private static Type GlobalRoutingMiddleware { get; set; }
        private static List<Type> GlobalPostRequestMiddlewares { get; } = new();
        private static List<Type> GlobalResponseMiddlewares { get; } = new();


        private Type ExceptionMiddleware { get; set; }
        private List<Type> LoggingMiddlewares { get; } = new();
        private List<Type> AuthenticationMiddlewares { get; } = new();
        private List<Type> AuthorizationMiddlewares { get; } = new();
        private List<Type> PreRequestMiddlewares { get; } = new();
        private List<Type> PostRequestMiddlewares { get; } = new();
        private List<Type> ResponseMiddlewares { get; } = new();

        private List<Type> BuiltMiddlewares { get; } = new();

        public IPipelineBuilder AddMiddleware<T>() where T : IMiddleware => AddMiddleware(typeof(T));
        public IPipelineBuilder AddMiddleware(Type middlewareType)
        {
            if (typeof(IExceptionMiddleware).IsAssignableFrom(middlewareType))
                ExceptionMiddleware = middlewareType;
            else if (typeof(ILoggingMiddleware).IsAssignableFrom(middlewareType))
                LoggingMiddlewares.Add(middlewareType);
            else if (typeof(IAuthenticationMiddleware).IsAssignableFrom(middlewareType))
                AuthenticationMiddlewares.Add(middlewareType);
            else if (typeof(IPreRequestMiddleware).IsAssignableFrom(middlewareType))
                PreRequestMiddlewares.Add(middlewareType);
            else if (typeof(IPostRequestMiddleware).IsAssignableFrom(middlewareType))
                PostRequestMiddlewares.Add(middlewareType);
            else if (typeof(IResponseMiddleware).IsAssignableFrom(middlewareType))
                ResponseMiddlewares.Add(middlewareType);
            else
                throw new NotImplementedException($"El tipo {middlewareType.FullName} no es un middleware válido.");

            return this;
        }

        public static void AddglobalMiddleware<T>() where T : IMiddleware => AddglobalMiddleware(typeof(T));
        public static void AddglobalMiddleware(Type middlewareType)
        {
            if (typeof(IInternalExceptionMiddleware).IsAssignableFrom(middlewareType))
                GlobalInternalExceptionMiddleware ??= middlewareType;
            else if (typeof(IExceptionMiddleware).IsAssignableFrom(middlewareType))
                GlobalExceptionMiddleware ??= middlewareType;
            else if (typeof(IInternalRoutingMiddleware).IsAssignableFrom(middlewareType))
                GlobalRoutingMiddleware ??= middlewareType;
            else if (typeof(ILoggingMiddleware).IsAssignableFrom(middlewareType))
                GlobalLoggingMiddlewares.Add(middlewareType);
            else if (typeof(IAuthenticationMiddleware).IsAssignableFrom(middlewareType))
                GlobalAuthenticationMiddlewares.Add(middlewareType);
            else if (typeof(IPreRequestMiddleware).IsAssignableFrom(middlewareType))
                GlobalPreRequestMiddlewares.Add(middlewareType);
            else if (typeof(IPostRequestMiddleware).IsAssignableFrom(middlewareType))
                GlobalPostRequestMiddlewares.Add(middlewareType);
            else if (typeof(IResponseMiddleware).IsAssignableFrom(middlewareType))
                GlobalResponseMiddlewares.Add(middlewareType);
            else
                throw new NotImplementedException($"El tipo {middlewareType.FullName} no es un middleware válido.");
        }

        public void UseGlobalMiddlewaresFirst(bool? value = null)
        {
            GlobalMiddlewaresFirst = value ?? true;
        }

        private List<Type> GetMiddlewares()
        {
            if(BuiltMiddlewares.Count > 0)
                return BuiltMiddlewares;

            var middlewares = new List<Type>();

            middlewares.Add(GlobalInternalExceptionMiddleware);
          
            if (GlobalMiddlewaresFirst)
            {
                middlewares.Add(GlobalExceptionMiddleware);
                middlewares.Add(ExceptionMiddleware);

                middlewares.AddRange(GlobalLoggingMiddlewares);
                middlewares.AddRange(LoggingMiddlewares);

                middlewares.AddRange(GlobalAuthenticationMiddlewares);
                middlewares.AddRange(AuthenticationMiddlewares);

                middlewares.AddRange(GlobalPreRequestMiddlewares);
                middlewares.AddRange(PreRequestMiddlewares);

                middlewares.AddRange(GlobalAuthorizationMiddlewares);
                middlewares.AddRange(AuthorizationMiddlewares);

                middlewares.Add(GlobalRoutingMiddleware);

                middlewares.AddRange(GlobalPostRequestMiddlewares);
                middlewares.AddRange(PostRequestMiddlewares);

                middlewares.AddRange(GlobalResponseMiddlewares);
                middlewares.AddRange(ResponseMiddlewares);
            }
            else
            {
                middlewares.Add(ExceptionMiddleware);
                middlewares.Add(GlobalExceptionMiddleware);

                middlewares.AddRange(LoggingMiddlewares);
                middlewares.AddRange(GlobalLoggingMiddlewares);

                middlewares.AddRange(AuthenticationMiddlewares);
                middlewares.AddRange(GlobalAuthenticationMiddlewares);

                middlewares.AddRange(PreRequestMiddlewares);
                middlewares.AddRange(GlobalPreRequestMiddlewares);

                middlewares.AddRange(AuthorizationMiddlewares);
                middlewares.AddRange(GlobalAuthorizationMiddlewares);

                middlewares.Add(GlobalRoutingMiddleware);

                middlewares.AddRange(PostRequestMiddlewares);
                middlewares.AddRange(GlobalPostRequestMiddlewares);

                middlewares.AddRange(ResponseMiddlewares);
                middlewares.AddRange(GlobalResponseMiddlewares);
            }

            var result = middlewares.Where(x => x != null);

            if(result.Any() && BuiltMiddlewares.Count == 0)
                BuiltMiddlewares.AddRange(result);

            return BuiltMiddlewares;
        }

        public IPipelineExecutor Build(IOperationRequest request, IOperationContext context, ResultHandlerDelegate resultHandler, IServiceProvider serviceProvider)
        {
            var middlewares = GetMiddlewares();

            PipelineDelegate currentDelegate = () => { return Task.FromResult(context); };

            foreach (Type middlewareType in middlewares.AsEnumerable().Reverse())
            {
                var next = currentDelegate;

                if (middlewareType.IsAssignableTo(typeof(IInternalRoutingMiddleware)))
                {
                    currentDelegate = () =>
                    {
                        var middleware = (IInternalRoutingMiddleware)serviceProvider.GetRequiredService(middlewareType);
                        return middleware.Execute(request, context, resultHandler, next);
                    };
                }
                else
                {
                    currentDelegate = () =>
                    {
                        var middleware = (IExecutableMiddleware)serviceProvider.GetRequiredService(middlewareType);
                        return middleware.Execute(request, context, next);
                    };
                }
            }

            async Task<IOperationContext> executionDelegate()
            {
                await currentDelegate.Invoke();
                return context;
            }

            return new PipelineExecutor(executionDelegate);
        }
    }
}