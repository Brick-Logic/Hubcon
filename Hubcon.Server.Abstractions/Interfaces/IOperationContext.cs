using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Hubcon
{
    public interface IOperationContext
    {
        IOperationBlueprint Blueprint { get; init; }
        Exception? Exception { get; set; }
        HttpContext? HttpContext { get; init; }
        IDictionary<string, object> Items { get; }
        string OperationName { get; init; }
        IOperationRequest Request { get; init; }
        object? WrappedRequest { get; init; }
        CancellationToken RequestAborted { get; init; }
        IServiceProvider RequestServices { get; init; }
        IHubconResponse Response { get; set; }
        ClaimsPrincipal? User { get; init; }
    }
}