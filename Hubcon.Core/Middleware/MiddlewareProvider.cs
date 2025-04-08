using Hubcon.Core.Models;
using Hubcon.Core.Models.Interfaces;
using Hubcon.Core.Models.Middleware;
using Hubcon.Core.Tools;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Core.Middleware
{
    internal class MiddlewareProvider : IMiddlewareProvider
    {
        private static Dictionary<Type, PipelineBuilder> PipelineBuilders = new();

        public void AddMiddlewares<TController>(Action<IPipelineOptions> options) where TController : IBaseHubconController
        {
            if (!PipelineBuilders.TryGetValue(typeof(TController), out PipelineBuilder? value))
                PipelineBuilders[typeof(TController)] = value = new PipelineBuilder();

            options?.Invoke(value!);
        }

        public IPipeline GetPipeline(Type controllerType, MethodInvokeRequest request, Func<Task<MethodResponse?>> handler)
        {
            if (!PipelineBuilders.TryGetValue(controllerType, out PipelineBuilder? value))
                PipelineBuilders[controllerType] = value = new PipelineBuilder();

            return PipelineBuilders[controllerType].Build(controllerType, request, handler);
        }
    }

    public interface IPipelineBuilder
    {
        public IPipelineBuilder AddMiddleware<T>() where T : IMiddleware;
        public IPipeline Build(Type controllerType, MethodInvokeRequest request, Func<Task<MethodResponse?>> handler);
    }

    public interface IPipelineOptions
    {
        public IPipelineBuilder AddMiddleware<T>() where T : IMiddleware;
    }

    public interface IPipeline
    {
        public Task<MethodResponse> Execute();
    }

    internal class Pipeline : IPipeline
    {
        public Pipeline(Type controllerType, Func<Task<MethodResponse?>> pipelineReference)
        {
            pipelineMethod = pipelineReference!;
        }

        private Func<Task<MethodResponse>> pipelineMethod { get; }

        public async Task<MethodResponse> Execute()
        {
            return await pipelineMethod();
        }
    }

    public class MainHandler : IMiddleware
    {
        private readonly Func<Task<MethodResponse?>> mainHandlerReference;

        public MainHandler(Func<Task<MethodResponse?>> mainHandlerReference)
        {
            this.mainHandlerReference = mainHandlerReference;
        }

        public async Task<MethodResponse?> Execute(MethodInvokeRequest request, Func<Task<MethodResponse?>> next)
        {
            return await mainHandlerReference.Invoke();
        }
    }

    internal class PipelineBuilder : IPipelineBuilder, IPipelineOptions
    {
        private List<Type> ExceptionMiddlewares { get; } = new();
        private List<Type> LoggingMiddlewares { get; } = new();
        private List<Type> AuthenticationMiddlewares { get; } = new();
        private List<Type> PreRequestMiddlewares { get; } = new();
        private List<Type> PostRequestMiddlewares { get; } = new();
        private Type? ResponseMiddleware { get; set; } = null!;

        public IPipelineBuilder AddMiddleware<T>() where T: IMiddleware
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
                if(ResponseMiddleware != null)
                    throw new NotImplementedException($"El tipo {ResponseMiddleware.FullName} ya está registrado como middleware de respuesta.");
                else
                    ResponseMiddleware = type;
            }
            else
                throw new NotImplementedException($"El tipo {type.FullName} no es un middleware válido.");
            return this;
        }

        public IPipeline Build(Type controllerType, MethodInvokeRequest request, Func<Task<MethodResponse?>> handler)
        {
            var preHandlerMiddlewares = new List<Type>();
            preHandlerMiddlewares.AddRange(ExceptionMiddlewares);
            preHandlerMiddlewares.AddRange(LoggingMiddlewares);
            preHandlerMiddlewares.AddRange(AuthenticationMiddlewares);
            preHandlerMiddlewares.AddRange(PreRequestMiddlewares);


            var middlewares = new List<IMiddleware>();
            foreach(var mw in preHandlerMiddlewares)
                middlewares.Add((IMiddleware)InstanceCreator.TryCreateInstance(mw)!);

            var postHandlerMiddlewares = new List<IPostRequestMiddleware>();
            foreach (var mw in PostRequestMiddlewares)
                postHandlerMiddlewares.Add((IPostRequestMiddleware)InstanceCreator.TryCreateInstance(mw)!);
            
            var loggingMiddlewares = new List<ILoggingMiddleware>();
            foreach (var mw in LoggingMiddlewares)
                loggingMiddlewares.Add((ILoggingMiddleware)InstanceCreator.TryCreateInstance(mw)!);


            Func<Task<MethodResponse?>> final = () => handler(); // el método original

            foreach (var mw in middlewares.Reverse<IMiddleware>())
            {
                var next = final;
                final = () => mw.Execute(request, next!);
            }

            Func<Task<MethodResponse?>> wrapped = async () =>
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
                        var responseMw = (IResponseMiddleware)InstanceCreator.TryCreateInstance(ResponseMiddleware)!;
                        response = await responseMw.Execute(response!);
                    }
                };

                foreach(var logMw in loggingMiddlewares)
                {
                    await logMw.Execute(request, response!, finish);
                }

                return response;
            };

            return new Pipeline(controllerType, wrapped!);
        }
    }
}
