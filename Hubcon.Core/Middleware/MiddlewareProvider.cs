using Autofac;
using Hubcon.Core.MethodHandling;
using Hubcon.Core.Models;
using Hubcon.Core.Models.Interfaces;
using Hubcon.Core.Models.Middleware;
using Hubcon.Core.Models.Pipeline;
using Hubcon.Core.Models.Pipeline.Interfaces;
using Hubcon.Core.Tools;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
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
        private readonly static Dictionary<Type, PipelineBuilder> PipelineBuilders = new();
        private readonly ILifetimeScope _serviceProvider;

        public MiddlewareProvider(ILifetimeScope serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public static void AddMiddlewares(Type controllerType, Action<IMiddlewareOptions>? options, List<Action<IMiddlewareOptions>> globalMiddlewares, List<Action<ContainerBuilder>> servicesToInject)
        {
            if (!PipelineBuilders.TryGetValue(controllerType, out PipelineBuilder? value))
            {
                PipelineBuilders[controllerType] = value = new PipelineBuilder();
            }
            var pipelineOptions = new MiddlewareOptions(value, servicesToInject);

            foreach (var middlewareOptions in globalMiddlewares)
                middlewareOptions?.Invoke(pipelineOptions);

            options?.Invoke(pipelineOptions);
        }

        public static void AddMiddlewares<TController>(Action<IMiddlewareOptions> options, List<Action<IMiddlewareOptions>> globalMiddlewares, List<Action<ContainerBuilder>> servicesToInject) where TController : IBaseHubconController
        {
            AddMiddlewares(typeof(TController), options, globalMiddlewares, servicesToInject);
        }

        public IPipeline GetPipeline(HubconMethodInvoker methodInvoker, MethodInvokeRequest request, Func<Task<IMethodResponse?>> handler)
        {
            if (!PipelineBuilders.TryGetValue(methodInvoker.ControllerType, out PipelineBuilder? value))
                PipelineBuilders[methodInvoker.ControllerType] = value = new PipelineBuilder();

            return PipelineBuilders[methodInvoker.ControllerType].Build(request, handler, _serviceProvider);
        }
    }
}
