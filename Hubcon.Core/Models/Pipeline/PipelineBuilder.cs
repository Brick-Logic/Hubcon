using Autofac;
using Hubcon.Core.Extensions;
using Hubcon.Core.Models.Middleware;
using Hubcon.Core.Models.Pipeline.Interfaces;
using Hubcon.Core.Tools;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Core.Models.Pipeline
{
    public class MiddlewareOptions : IMiddlewareOptions
    {
        IPipelineBuilder _builder;
        public List<Action<ContainerBuilder>> ServicesToInject;

        internal MiddlewareOptions(PipelineBuilder builder, List<Action<ContainerBuilder>> servicesToInject)
        {
            _builder = builder;
            ServicesToInject = servicesToInject;
        }

        public IMiddlewareOptions AddMiddleware<T>() where T : class, IMiddleware
        {
            return AddMiddleware(typeof(T));
        }

        public IMiddlewareOptions AddMiddleware(Type middlewareType)
        {
            _builder.AddMiddleware(middlewareType);
            ServicesToInject.Add(x => x.RegisterWithInjector(y => y.RegisterType(middlewareType)));
            return this;
        }
    }

    internal class PipelineBuilder : IPipelineBuilder
    {
        private List<Type> ExceptionMiddlewares { get; } = new();
        private List<Type> LoggingMiddlewares { get; } = new();
        private List<Type> AuthenticationMiddlewares { get; } = new();
        private List<Type> PreRequestMiddlewares { get; } = new();
        private List<Type> PostRequestMiddlewares { get; } = new();
        private List<Type> ResponseMiddlewares { get; set; } = new();

        public IPipelineBuilder AddMiddleware(Type middlewareType)
        {
            if (typeof(IExceptionMiddleware).IsAssignableFrom(middlewareType))
                ExceptionMiddlewares.Add(middlewareType);
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
        public IPipelineBuilder AddMiddleware<T>() where T : IMiddleware => AddMiddleware(typeof(T));

        public IPipeline Build(MethodInvokeRequest request, Func<Task<IMethodResponse?>> handler, ILifetimeScope serviceProvider)
        {
            var preHandlerMiddlewares = new List<Type>();
            preHandlerMiddlewares.AddRange(ExceptionMiddlewares);
            preHandlerMiddlewares.AddRange(LoggingMiddlewares);
            preHandlerMiddlewares.AddRange(AuthenticationMiddlewares);
            preHandlerMiddlewares.AddRange(PreRequestMiddlewares);

            var middlewares = new List<IMiddleware>();
            foreach (var mw in preHandlerMiddlewares)
                middlewares.Add((IMiddleware)serviceProvider.Resolve(mw));

            var postHandlerMiddlewares = new List<IPostRequestMiddleware>();
            foreach (var mw in PostRequestMiddlewares)
                postHandlerMiddlewares.Add((IPostRequestMiddleware)serviceProvider.Resolve(mw));

            var loggingMiddlewares = new List<ILoggingMiddleware>();
            foreach (var mw in LoggingMiddlewares)
                loggingMiddlewares.Add((ILoggingMiddleware)serviceProvider.Resolve(mw));


            Func<Task<IMethodResponse?>> final = () => handler(); // el método original

            foreach (var mw in middlewares.Reverse<IMiddleware>())
            {
                var next = final;
                final = () => mw.Execute(request, next!);
            }

            Func<Task<IMethodResponse?>> wrapped = async () =>
            {
                var response = await final();

                var finish = async () =>
                {

                    foreach (var postMw in postHandlerMiddlewares)
                    {
                        response = await postMw.Execute(request, response!);
                    }
                    
                    foreach(var middleware in ResponseMiddlewares)
                    {
                        var responseMw = (IResponseMiddleware)serviceProvider.Resolve(middleware);
                        response = await responseMw.Execute(response!);
                    }
                    
                };

                foreach (var logMw in loggingMiddlewares)
                {
                    await logMw.Execute(request, response!, finish);
                }

                return response;
            };

            return new Pipeline(wrapped!);
        }
    }
}
