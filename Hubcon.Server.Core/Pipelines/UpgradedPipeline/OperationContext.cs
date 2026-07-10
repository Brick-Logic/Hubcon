using Hubcon.Server.Abstractions.Delegates;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Standard.Interfaces;
using Microsoft.AspNetCore.Http;
using System.Runtime.InteropServices;
using System.Security.Claims;

namespace Hubcon.Server.Core.Pipelines.UpgradedPipeline
{
    [StructLayout(LayoutKind.Sequential)]
    internal sealed class OperationContext : IOperationContext
    {
        public IOperationBlueprint Blueprint { get; init; } = default!;
        public IOperationRequest Request { get; init; } = default!;
        public IServiceProvider RequestServices { get; init; } = default!;
        public IHubconResponse Response { get; set; } = default!;
        public ResultHandlerDelegate ResultHandler { get; internal set; }
        public ClaimsPrincipal? User { get; set; }
        public string OperationName { get; init; } = string.Empty;
        public IDictionary<string, object> Items { get; } = new Dictionary<string, object>();
        public HttpContext? HttpContext { get; init; } = default!;
        public Exception? Exception { get; set; } = default!;
        public CancellationToken RequestAborted { get; init; } = default!;
        public IWrapper? WrappedRequest { get; init; } = default!;
        public bool IsTransportCalled { get; internal set; }
    }
}
