using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hubcon.Server.Core.Routing.Models;

// public sealed class HttpHubconResult : IResult
// {
//     private readonly IHubconResponse _response;
//
//     public HttpHubconResult(IHubconResponse  response)
//     {
//         _response = response;
//     }
//     
//     public Task ExecuteAsync(HttpContext httpContext)
//     {
//         var converter = httpContext.RequestServices.GetRequiredService<IDynamicConverter>();
//
//         var response = converter.Serialize(_response);
//         Console.WriteLine(response);
//         
//         httpContext.Response.StatusCode = _response.StatusCode;
//         httpContext.Response.ContentType = "application/json";
//         httpContext.Response.WriteAsync(response);
//
//         return Task.CompletedTask;
//     }
// }

public class HttpHubconResult : IResult
{
    private readonly IResponse _response;
    private readonly Type _responseType;

    public HttpHubconResult(IResponse response, Type responseType)
    {
        _response = response;
        _responseType = responseType;
    }
    
    public Task ExecuteAsync(HttpContext httpContext)
    {
        var converter = httpContext.RequestServices.GetRequiredService<IDynamicConverter>();
        var logger = httpContext.RequestServices.GetRequiredService<ILogger<HttpHubconResult>>();
        var response = converter.Serialize(_response.GetOriginal(), _responseType);
        logger.LogWarning(response);
        
        httpContext.Response.StatusCode = _response.StatusCode;
        httpContext.Response.ContentType = "application/json";
        httpContext.Response.WriteAsync(response);

        return Task.CompletedTask;
    }
}