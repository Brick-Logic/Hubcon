using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Standard.Interfaces;

using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Hubcon.Server.Abstractions.Interfaces
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