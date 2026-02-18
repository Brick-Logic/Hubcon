using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Standard.Interfaces;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Hubcon.Shared.Abstractions.Models
{
    public interface IInvocationContext
    {
        public IServiceProvider Services { get; }
        public IOperationRequest Request { get; }
        public CancellationToken CancellationToken { get; }
        public bool IsSuccess { get; }
        public int StatusCode { get; }
        public Exception? Exception { get; }
        public IAuthenticationManager AuthenticationManager { get; }
        public bool HasError { get; }

        public void SetException(Exception ex);
    }
}