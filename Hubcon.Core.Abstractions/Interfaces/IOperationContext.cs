using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Hubcon.Core.Abstractions.Interfaces
{
    public interface IOperationContext
    {
        object?[] Arguments { get; init; }
        IOperationBlueprint Blueprint { get; init; }
        Exception? Exception { get; set; }
        HttpContext? HttpContext { get; init; }
        IDictionary<string, object> Items { get; }
        string OperationName { get; init; }
        IOperationRequest Request { get; init; }
        CancellationToken RequestAborted { get; init; }
        IServiceProvider RequestServices { get; init; }
        IOperationResult? Result { get; set; }
        ClaimsPrincipal? User { get; init; }
    }
}