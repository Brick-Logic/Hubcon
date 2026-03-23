#pragma warning disable CS1591
using Hubcon.Shared.Abstractions.Interfaces;
using System;
using System.Threading;

namespace Hubcon.Shared.Abstractions.Models
{
    public sealed class RequestValidationContext
    {
        public RequestValidationContext(
            IServiceProvider Services,
            IOperationRequest Request,
            CancellationToken CancellationToken)
        {
            this.Services = Services;
            this.Request = Request;
            this.CancellationToken = CancellationToken;
        }

        public IServiceProvider Services { get; }
        public IOperationRequest Request { get; }
        public CancellationToken CancellationToken { get; }
    }
}