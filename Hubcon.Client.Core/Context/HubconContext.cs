using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Core.Context;
using Microsoft.AspNetCore.Http;
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

    public static class InterceptorContext
    {
        private static readonly AsyncLocal<InterceptorManager> _current = new();
        public static InterceptorManager Current => _current.Value;

        public static void UseContext(InterceptorManager context) => _current.Value = context;
    }

    public static class WrappedContext
    {
        private static readonly AsyncLocal<WrappedEnvelope> _current = new();

        //private static readonly AsyncLocal<bool> _currentWrapped = new();
        private static readonly AsyncLocal<bool> _currentWrappedSet = new();

        public static bool Current { get => _current.Value?.IsWrapped ?? false; }
        public static WrappedEnvelope CurrentWrapped { get => _current.Value ??= new(); }

        public static void UseWrapped(WrappedEnvelope wrappedEnvelope) => _current.Value ??= wrappedEnvelope;

        public static void SetWrapped(bool wrapped)
        {
            if (_current.Value is null || _currentWrappedSet.Value == true) return;

            _current.Value.IsWrapped = wrapped;
            _currentWrappedSet.Value = true;
        }
    }
}

namespace Hubcon.Shared.Core.Context
{
    public sealed class WrappedEnvelope
    {
        public bool ResponseIsSet => Response != null;
        private object? Response { get; set; }
        public bool IsWrapped { get; internal set; }

        public void SetResponse<T>(IHubconResponse<T> response)
        {
            Response ??= response;
        }

        public void SetResponse(IResponse response)
        {
            Response ??= response;
        }

        public IHubconResponse<T> GetResponse<T>()
        {
            return Response as IHubconResponse<T> ?? HubconResponse.Fail<T>("Empty response");
        }

        public object? GetRawResponse()
        {
            return Response;
        }
    }

    public sealed class InterceptorManager : IInterceptorManager
    {
        public InterceptorManager(IServiceProvider serviceProvider, IClientOptions clientOptions, IContractOptions contractOptions, IOperationOptions? operationOptions = null, IInvocationContext? context = null)
        {
            ServiceProvider = serviceProvider;
            ClientOptions = clientOptions;
            ContractOptions = contractOptions;
            OperationOptions = operationOptions;
            Request = context?.Request;
            _context = context!;
        }

        public IServiceProvider ServiceProvider { get; }
        public IClientOptions ClientOptions { get; }
        public IContractOptions ContractOptions { get; }
        public IOperationOptions? OperationOptions { get; }
        public IOperationRequest? Request { get; }

        private IInvocationContext? _context;

        private IInvocationContext GetContext(CancellationToken cancellationToken)
        {
            var context = _context as CallContext;
            if (context != null) context.CancellationToken = cancellationToken;

            return context ?? new CallContext(
                ServiceProvider,
                null!,
                ClientOptions.AuthenticationManagerFactory?.GetValue<IAuthenticationManager>(ServiceProvider)!,
                WrappedContext.Current,
                cancellationToken
            );
        }

        public async Task CallValidationHooks(CancellationToken cancellationToken = default)
        {
            if (OperationOptions == null)
                return;

            await OperationOptions.CallValidationHook(ServiceProvider, Request!, cancellationToken);
        }

        public async Task CallInterceptor(InterceptorType interceptorType, CancellationToken cancellationToken = default)
        {
            var context = GetContext(cancellationToken);
            await ClientOptions.CallInterceptor(interceptorType, context);
        }

        public async Task CallHooks(HookType hookType, CancellationToken cancellationToken = default)
        {
            var context = GetContext(cancellationToken);
            if (OperationOptions != null) await OperationOptions.CallHook(hookType, context);
            await ContractOptions.CallHook(hookType, context);
        }

        public async Task CallHooksAndInterceptors(HookType hookType, CancellationToken cancellationToken = default)
        {
            var context = GetContext(cancellationToken);
            if (OperationOptions != null) await OperationOptions.CallHook(hookType, context);
            await ContractOptions.CallHook(hookType, context);

            if (Enum.TryParse<InterceptorType>(hookType.ToString(), true, out var value))
            {
                await ClientOptions.CallInterceptor(value, context);
            }
        }
    }

    public sealed class CallContext : IInvocationContext
    {

        public CallContext(
            IServiceProvider services,
            IOperationRequest request,
            IAuthenticationManager authenticationManager,
            bool isWrapped,
            CancellationToken cancellationToken)
        {
            Services = services;
            Request = request;
            AuthenticationManager = authenticationManager;
            this.CancellationToken = cancellationToken;
            IsWrapped = isWrapped;
        }

        public IServiceProvider Services { get; private set; }
        public IOperationRequest Request { get; }
        public IAuthenticationManager AuthenticationManager { get; }
        public CancellationToken CancellationToken { get; internal set; }
        public Func<string, Task<HubconResponse<bool>>>? TryRefreshToken { get; set; }

        public bool IsWrapped { get; private set; }


        public bool HasError => Exception != null;
        public Exception? Exception { get; private set; }
        public bool IsSuccess { get; set; }
        public int StatusCode { get; set; }

        public void SetException(Exception ex)
        {
            Exception ??= ex;
            IsSuccess = false;
        }
    }
}
