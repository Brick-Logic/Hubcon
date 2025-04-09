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
        private readonly IServiceProvider _serviceProvider;

        public MiddlewareProvider(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public static void AddMiddlewares<TController>(Action<IPipelineOptions> options, IServiceCollection services) where TController : IBaseHubconController
        {
            if (!PipelineBuilders.TryGetValue(typeof(TController), out PipelineBuilder? value))
            {
                PipelineBuilders[typeof(TController)] = value = new PipelineBuilder();
            }
                var pipelineOptions = new PipelineOptions(services, value);
                options?.Invoke(pipelineOptions);
        }

        public IPipeline GetPipeline(Type controllerType, MethodInvokeRequest request, Func<Task<MethodResponse?>> handler)
        {
            if (!PipelineBuilders.TryGetValue(controllerType, out PipelineBuilder? value))
                PipelineBuilders[controllerType] = value = new PipelineBuilder();

            return PipelineBuilders[controllerType].Build(controllerType, request, handler, _serviceProvider);
        }
    }
}
