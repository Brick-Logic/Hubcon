using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Core.Context;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Hubcon
{
    public static class HubconContext
    {
        private static readonly AsyncLocal<CallContext> _current = new();
        public static CallContext Current => _current.Value;

        public static void UseContext(CallContext context) => _current.Value = context;
    }

    public static class WrappedContext
    {
        private static readonly AsyncLocal<bool> _current = new();
        private static readonly AsyncLocal<bool> _currentSet = new();
        public static bool Current { get => _current.Value; }

        public static void SetWrapped(bool wrapped)
        {
            if(_currentSet.Value == true) return;

            _current.Value = wrapped;
            _currentSet.Value = true;
        }
    }
}

namespace Hubcon.Shared.Core.Context
{
    public sealed class CallContext : IInvocationContext
    {
        public CallContext(IServiceProvider services, IOperationRequest request, IAuthenticationManager authenticationManager, bool isWrapped, CancellationToken cancellationToken)
        {
            Services = services;
            Request = request;
            AuthenticationManager = authenticationManager;
            this.CancellationToken = cancellationToken;
            IsWrapped = isWrapped;
        }

        public IServiceProvider Services { get; }
        public IOperationRequest Request { get; }
        public IAuthenticationManager AuthenticationManager { get; }
        public CancellationToken CancellationToken { get; }

        public bool IsWrapped { get; private set; }

        public Func<string, Task<HubconResponse<bool>>>? TryRefreshToken { get; set; }

        public bool HasError => Exception != null;
        public Exception? Exception { get; private set; }
        public bool ResponseIsSet => Response != null;
        private object? Response { get; set; }

        public bool IsSuccess { get; set; }
        public int StatusCode { get; set; }

        public void SetResponse<T>(IHubconResponse<T> response)
        {
            Response ??= response;
            IsSuccess = response.Success;
            StatusCode = response.StatusCode;
        }

        public void SetException(Exception ex)
        {
            Exception ??= ex;
            IsSuccess = false;
        }

        public IHubconResponse<T>? GetResponse<T>()
        {
            return Response as IHubconResponse<T>;
        }
    }
}
