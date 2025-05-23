namespace Hubcon.Server.Components.Pipelines
{
    //public class PipelineBuilder : IPipelineBuilder
    //{
    //    private List<Type> ExceptionMiddlewares { get; } = new();
    //    private List<Type> LoggingMiddlewares { get; } = new();
    //    private List<Type> AuthenticationMiddlewares { get; } = new();
    //    private List<Type> PreRequestMiddlewares { get; } = new();
    //    private List<Type> PostRequestMiddlewares { get; } = new();
    //    private List<Type> ResponseMiddlewares { get; set; } = new();

    //    public IPipelineBuilder AddMiddleware(Type middlewareType)
    //    {
    //        if (typeof(IExceptionMiddleware).IsAssignableFrom(middlewareType))
    //            ExceptionMiddlewares.Add(middlewareType);
    //        else if (typeof(ILoggingMiddleware).IsAssignableFrom(middlewareType))
    //            LoggingMiddlewares.Add(middlewareType);
    //        else if (typeof(IAuthenticationMiddleware).IsAssignableFrom(middlewareType))
    //            AuthenticationMiddlewares.Add(middlewareType);
    //        else if (typeof(IPreRequestMiddleware).IsAssignableFrom(middlewareType))
    //            PreRequestMiddlewares.Add(middlewareType);
    //        else if (typeof(IPostRequestMiddleware).IsAssignableFrom(middlewareType))
    //            PostRequestMiddlewares.Add(middlewareType);
    //        else if (typeof(IResponseMiddleware).IsAssignableFrom(middlewareType))
    //            ResponseMiddlewares.Add(middlewareType);
    //        else
    //            throw new NotImplementedException($"El tipo {middlewareType.FullName} no es un middleware válido.");

    //        return this;
    //    }
    //    public IPipelineBuilder AddMiddleware<T>() where T : IMiddleware => AddMiddleware(typeof(T));

    //    public IPipeline Build(IOperationRequest request, PipelineDelegate handler, ILifetimeScope serviceProvider)
    //    {
    //        return new Pipeline(default!);
        
    //    }

    //    public IPipeline Build(IOperationRequest request, PipelineDelegate handler, IServiceProvider serviceProvider)
    //    {
    //        throw new NotImplementedException();
    //    }
    //    //    var preHandlerMiddlewares = new List<Type>();
    //    //    preHandlerMiddlewares.AddRange(ExceptionMiddlewares);
    //    //    preHandlerMiddlewares.AddRange(LoggingMiddlewares);
    //    //    preHandlerMiddlewares.AddRange(AuthenticationMiddlewares);
    //    //    preHandlerMiddlewares.AddRange(PreRequestMiddlewares);

    //    //    var middlewares = new List<IMiddleware>();
    //    //    foreach (var mw in preHandlerMiddlewares)
    //    //        middlewares.Add((IMiddleware)serviceProvider.Resolve(mw));

    //    //    var postHandlerMiddlewares = new List<IPostRequestMiddleware>();
    //    //    foreach (var mw in PostRequestMiddlewares)
    //    //        postHandlerMiddlewares.Add((IPostRequestMiddleware)serviceProvider.Resolve(mw));

    //    //    var loggingMiddlewares = new List<ILoggingMiddleware>();
    //    //    foreach (var mw in LoggingMiddlewares)
    //    //        loggingMiddlewares.Add((ILoggingMiddleware)serviceProvider.Resolve(mw));


    //    //    PipelineDelegate final = () => handler(); // el método original

    //    //    foreach (var mw in middlewares.Reverse<IMiddleware>())
    //    //    {
    //    //        var next = final;
    //    //        final = () => mw.Execute(request, next!);
    //    //    }

    //    //    Func<Task<IObjectMethodResponse?>> wrapped = async () =>
    //    //    {
    //    //        var response = await final();

    //    //        var finish = async () =>
    //    //        {

    //    //            foreach (var postMw in postHandlerMiddlewares)
    //    //            {
    //    //                response = await postMw.Execute(request, response!);
    //    //            }

    //    //            foreach(var middleware in ResponseMiddlewares)
    //    //            {
    //    //                var responseMw = (IResponseMiddleware)serviceProvider.Resolve(middleware);
    //    //                response = await responseMw.Execute(response!);
    //    //            }

    //    //        };

    //    //        foreach (var logMw in loggingMiddlewares)
    //    //        {
    //    //            await logMw.Execute(request, response!, finish);
    //    //        }

    //    //        return response;
    //    //    };

    //    //    return new Pipeline(wrapped!);
    //    //}
    //}
}
