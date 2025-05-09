using Autofac;
using Hubcon.Core.Abstractions.Delegates;
using Hubcon.Core.Abstractions.Interfaces;
using Hubcon.Core.Invocation;
using Hubcon.Core.Pipelines;

namespace Hubcon.Core.Middlewares
{
    public class MiddlewareProvider : IMiddlewareProvider
    {
        private readonly static Dictionary<Type, IPipelineBuilder> PipelineBuilders = new();
        private readonly ILifetimeScope _serviceProvider;

        public MiddlewareProvider(ILifetimeScope serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public static void AddMiddlewares(Type controllerType, Action<IMiddlewareOptions>? options, List<Action<IMiddlewareOptions>> globalMiddlewares, List<Action<ContainerBuilder>> servicesToInject)
        {
            if (!PipelineBuilders.TryGetValue(controllerType, out IPipelineBuilder? value))
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

        public IPipeline GetPipeline(IMethodDescriptor descriptor, IMethodInvokeRequest request, InvocationDelegate handler)
        {
            if (!PipelineBuilders.TryGetValue(descriptor.ControllerType, out IPipelineBuilder? value))
                PipelineBuilders[descriptor.ControllerType] = value = new PipelineBuilder();

            return PipelineBuilders[descriptor.ControllerType].Build(request, handler, _serviceProvider);
        }
    }
}
