using Hubcon.Server.Abstractions.Delegates;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Abstractions.Models;
using Hubcon.Server.Core.Middlewares.DefaultMiddlewares;
using Hubcon.Shared.Abstractions.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
#pragma warning disable CS1591

namespace Hubcon.Server.Core.Pipelines.UpgradedPipeline
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class PipelineBuilder : IPipelineBuilder
    {
        private static bool GlobalMiddlewaresFirst { get; set; } = false;

        private static Type? GlobalInternalExceptionMiddleware { get; set; }
        private static Type? GlobalExceptionMiddleware { get; set; }
        private static List<Type> GlobalTelemetryMiddlewares { get; } = new();
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

        private Type[] BuiltMiddlewares { get; set; } = [];

        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);

        public IPipelineBuilder AddMiddleware<T>() where T : IMiddleware => AddMiddleware(typeof(T));
        public IPipelineBuilder AddMiddleware(Type middlewareType)
        {
            if (typeof(IExceptionMiddleware).IsAssignableFrom(middlewareType))
                ExceptionMiddleware = middlewareType;
            else if (typeof(ITelemetryMiddleware).IsAssignableFrom(middlewareType))
                GlobalTelemetryMiddlewares.Add(middlewareType);
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

        public static void AddglobalMiddleware<T>() where T : IMiddleware => AddGlobalMiddleware(typeof(T));
        public static void AddGlobalMiddleware(Type middlewareType)
        {
            if (typeof(IInternalExceptionMiddleware).IsAssignableFrom(middlewareType))
                GlobalInternalExceptionMiddleware ??= middlewareType;
            else if (typeof(IExceptionMiddleware).IsAssignableFrom(middlewareType))
                GlobalExceptionMiddleware ??= middlewareType;
            else if (typeof(ITelemetryMiddleware).IsAssignableFrom(middlewareType))
                GlobalTelemetryMiddlewares.Add(middlewareType);
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

        private Type[] GetMiddlewares()
        {
            if (BuiltMiddlewares.Length > 0)
                return BuiltMiddlewares;

            semaphore.Wait();

            if (BuiltMiddlewares.Length > 0)
                return BuiltMiddlewares;

            var middlewares = new List<Type>();

            if(GlobalInternalExceptionMiddleware != null) middlewares.Add(GlobalInternalExceptionMiddleware);

            if (GlobalMiddlewaresFirst)
            {
                if (GlobalExceptionMiddleware != null)
                    middlewares.Add(GlobalExceptionMiddleware);

                if (ExceptionMiddleware != null)
                    middlewares.Add(ExceptionMiddleware);

                middlewares.AddRange(GlobalTelemetryMiddlewares);

                middlewares.AddRange(GlobalLoggingMiddlewares);
                middlewares.AddRange(LoggingMiddlewares);

                middlewares.AddRange(GlobalAuthenticationMiddlewares);
                middlewares.AddRange(AuthenticationMiddlewares);

                middlewares.AddRange(GlobalPreRequestMiddlewares);
                middlewares.AddRange(PreRequestMiddlewares);

                middlewares.AddRange(GlobalAuthorizationMiddlewares);
                middlewares.AddRange(AuthorizationMiddlewares);

                if (GlobalRoutingMiddleware != null)
                    middlewares.Add(GlobalRoutingMiddleware);

                middlewares.AddRange(GlobalPostRequestMiddlewares);
                middlewares.AddRange(PostRequestMiddlewares);

                middlewares.AddRange(GlobalResponseMiddlewares);
                middlewares.AddRange(ResponseMiddlewares);
            }
            else
            {
                if(ExceptionMiddleware != null)
                    middlewares.Add(ExceptionMiddleware);

                if(GlobalExceptionMiddleware != null)
                    middlewares.Add(GlobalExceptionMiddleware);

                middlewares.AddRange(GlobalTelemetryMiddlewares);

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

            if (result.Any() && BuiltMiddlewares.Length == 0)
                BuiltMiddlewares = result.ToHashSet().ToArray();

            semaphore.Release();

            return BuiltMiddlewares;
        }

        public IPipelineExecutor Build(IOperationRequest request, IOperationContext context, IServiceProvider serviceProvider)
        {
            var middlewares = GetMiddlewares();

            //PipelineDelegate current = () => Task.CompletedTask;

            //foreach (var type in middlewares)
            //{
            //    var next = current;
            //    var middleware = (IExecutableMiddleware)serviceProvider.GetRequiredService(type);
            //    current = () => middleware.Execute(request, context, next);
            //}

            var state = new PipelineState()
            {
                Context = context,
                Middlewares = middlewares,
                Request = request,
                ServiceProvider = serviceProvider,
                Chain = InvokeNext
            };

            return new PipelineExecutor(state);
        }

        static Task InvokeNext(PipelineState state)
        {
            if (state.Index >= state.Middlewares.Length)
                return Task.CompletedTask;

            var type = state.Middlewares[state.Index++];

            var middleware = (IExecutableMiddleware)state.ServiceProvider.GetService(type)!;

            return middleware.Execute(
                state.Request,
                state.Context,
                () => InvokeNext(state)
            );
        }
    }
}