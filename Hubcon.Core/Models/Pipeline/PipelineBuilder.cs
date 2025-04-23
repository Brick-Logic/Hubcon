using Autofac;
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
    public class PipelineOptions : IMiddlewareOptions
    {
        IPipelineBuilder _builder;
        public List<Action<ContainerBuilder>> ServicesToInject;

        internal PipelineOptions(PipelineBuilder builder, List<Action<ContainerBuilder>> servicesToInject)
        {
            _builder = builder;
            ServicesToInject = servicesToInject;
        }

        public IMiddlewareOptions AddMiddleware<T>() where T : class, IMiddleware
        {
            _builder.AddMiddleware<T>();
            ServicesToInject.Add(x => x.RegisterWithInjector(y => y.RegisterType<T>()));
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
        private Type? ResponseMiddleware { get; set; } = null!;

        public IPipelineBuilder AddMiddleware<T>() where T : IMiddleware
        {
            var type = typeof(T);

            if (typeof(IExceptionMiddleware).IsAssignableFrom(type))
                ExceptionMiddlewares.Add(type);
            else if (typeof(ILoggingMiddleware).IsAssignableFrom(type))
                LoggingMiddlewares.Add(type);
            else if (typeof(IAuthenticationMiddleware).IsAssignableFrom(type))
                AuthenticationMiddlewares.Add(type);
            else if (typeof(IPreRequestMiddleware).IsAssignableFrom(type))
                PreRequestMiddlewares.Add(type);
            else if (typeof(IPostRequestMiddleware).IsAssignableFrom(type))
                PostRequestMiddlewares.Add(type);
            else if (typeof(IResponseMiddleware).IsAssignableFrom(type))
            {
                if (ResponseMiddleware != null)
                    throw new NotImplementedException($"El tipo {ResponseMiddleware.FullName} ya está registrado como middleware de respuesta.");
                else
                    ResponseMiddleware = type;
            }
            else
                throw new NotImplementedException($"El tipo {type.FullName} no es un middleware válido.");


            return this;
        }

        public IPipeline Build(Type controllerType, MethodInvokeRequest request, Func<Task<IMethodResponse?>> handler, ILifetimeScope serviceProvider)
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

                    if (ResponseMiddleware != null)
                    {
                        var responseMw = (IResponseMiddleware)serviceProvider.Resolve(ResponseMiddleware);
                        response = await responseMw.Execute(response!);
                    }
                };

                foreach (var logMw in loggingMiddlewares)
                {
                    await logMw.Execute(request, response!, finish);
                }

                return response;
            };

            return new Pipeline(controllerType, wrapped!);
        }
    }
}
