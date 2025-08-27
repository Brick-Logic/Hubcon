namespace HubconTest.Filters
{
    public class LoggingEndpointFilter : IEndpointFilter
    {
        public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
        {
            Console.WriteLine("Filtro solo en este endpoint");

            return await next(context);
        }
    }

    public class ClassLoggingEndpointFilter : IEndpointFilter
    {
        public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
        {
            Console.WriteLine("Filtro desde la clase");

            return await next(context);
        }
    }
}
