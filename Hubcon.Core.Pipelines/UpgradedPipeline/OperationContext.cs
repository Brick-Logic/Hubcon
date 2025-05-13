using Hubcon.Core.Abstractions.Delegates;
using Hubcon.Core.Abstractions.Interfaces;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Core.Pipelines.UpgradedPipeline
{
    public class OperationContext : IOperationContext
    {
        public string OperationName { get; init; }
        public IServiceProvider RequestServices { get; init; }
        public IOperationBlueprint Blueprint { get; init; }
        public object?[] Arguments { get; init; } = Array.Empty<object?>();
        public ClaimsPrincipal? User { get; init; }
        public IDictionary<string, object> Items { get; } = new Dictionary<string, object>();
        public IOperationRequest Request { get; init; }
        public IOperationResult? Result { get; set; }
        public HttpContext? HttpContext { get; init; }
        public Exception? Exception { get; set; }
        public CancellationToken RequestAborted { get; init; }
    }
}
