namespace Hubcon.Server.Core.Middlewares
{
    //public class MiddlewareProvider : IMiddlewareProvider
    //{
    //    private readonly static Dictionary<Type, IPipelineBuilder> PipelineBuilders = new();

    //    public static void AddMiddlewares(Type controllerType, Action<IMiddlewareOptions>? options, List<Action<IMiddlewareOptions>> globalMiddlewares, List<Action<ContainerBuilder>> servicesToInject)
    //    {
    //        if (!PipelineBuilders.TryGetValue(controllerType, out IPipelineBuilder? value))
    //        {
    //            PipelineBuilders[controllerType] = value = new PipelineBuilder();
    //        }

    //        var pipelineOptions = new MiddlewareOptions(value, servicesToInject);

    //        foreach (var middlewareOptions in globalMiddlewares)
    //            middlewareOptions?.Invoke(pipelineOptions);

    //        options?.Invoke(pipelineOptions);
    //    }

    //    public static void AddMiddlewares<TController>(Action<IMiddlewareOptions> options, List<Action<IMiddlewareOptions>> globalMiddlewares, List<Action<ContainerBuilder>> servicesToInject) where TController : IBaseHubconController
    //    {
    //        AddMiddlewares(typeof(TController), options, globalMiddlewares, servicesToInject);
    //    }

    //    public IPipelineExecutor GetPipeline(IOperationBlueprint blueprint, IOperationRequest request, IServiceProvider serviceProvider, PipelineDelegate handler)
    //    {
    //        if (!PipelineBuilders.TryGetValue(blueprint.ControllerType, out IPipelineBuilder? value))
    //            PipelineBuilders[blueprint.ControllerType] = value = new PipelineBuilder();

    //        return PipelineBuilders[blueprint.ControllerType].Build(request, handler, serviceProvider);
    //    }
    //}
}
