using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Standard.Interfaces;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Hubcon.Server.Core.Pipelines.UpgradedPipeline
{
    internal sealed class OperationContext : IOperationContext
    {
        public string OperationName { get; init; } = string.Empty;
        public IServiceProvider RequestServices { get; init; } = default!;
        public IOperationBlueprint Blueprint { get; init; } = default!;
        public ClaimsPrincipal? User { get; set; }
        public IDictionary<string, object> Items { get; } = new Dictionary<string, object>();
        public IOperationRequest Request { get; init; } = default!;
        public HttpContext? HttpContext { get; init; } = default!;
        public Exception? Exception { get; set; } = default!;
        public CancellationToken RequestAborted { get; init; } = default!;
        public object? WrappedRequest { get; init; } = default!;
        public IHubconResponse Response { get; set; } = default!;
        public bool IsTransportCalled { get; internal set; }
    }
}
