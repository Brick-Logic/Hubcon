using Hubcon.Shared.Abstractions.Interfaces;
using System;

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