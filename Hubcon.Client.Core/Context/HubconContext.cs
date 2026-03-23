using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Abstractions.Standard.Interfaces;
using Hubcon.Shared.Core.Context;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Hubcon
{
    /// <summary>
    /// Internal hubcon context.
    /// </summary>
    public static class HubconContext
    {
        private static readonly AsyncLocal<CallContext> _current = new();

        /// <summary>
        /// Current context of the call.
        /// </summary>
        public static CallContext Current => _current.Value;

        /// <summary>
        /// Sets the call context.
        /// </summary>
        /// <param name="context"></param>
        public static void UseContext(CallContext context) => _current.Value = context;
    }

    /// <summary>
    /// The context of the current interceptor.
    /// </summary>
    public static class InterceptorContext
    {
        private static readonly AsyncLocal<InterceptorManager> _current = new();

        /// <summary>
        /// The current instance of the interceptor manager.
        /// </summary>
        public static InterceptorManager Current => _current.Value;

        /// <summary>
        /// Sets the interceptor manager context.
        /// </summary>
        /// <param name="context"></param>
        public static void UseContext(InterceptorManager context) => _current.Value = context;
    }

    /// <summary>
    /// Wrapped call context.
    /// </summary>
    public static class WrappedContext
    {
        private static readonly AsyncLocal<WrappedEnvelope> _current = new();
        private static readonly AsyncLocal<bool> _currentWrappedSet = new();

        /// <summary>
        /// Determines if the context is wrapped in an Execute call.
        /// </summary>
        public static bool Current { get => _current.Value?.IsWrapped ?? false; }

        /// <summary>
        /// The current wrapped envelope.
        /// </summary>
        public static WrappedEnvelope CurrentWrapped { get => _current.Value ??= new(); }

        /// <summary>
        /// Sets the wrapped envelope.
        /// </summary>
        /// <param name="wrappedEnvelope"></param>
        public static void UseWrapped(WrappedEnvelope wrappedEnvelope) => _current.Value ??= wrappedEnvelope;

        /// <summary>
        /// Sets if the call is wrapped or not.
        /// </summary>
        /// <param name="wrapped"></param>
        public static void SetWrapped(bool wrapped)
        {
            if (_currentWrappedSet.Value == true) return;

            CurrentWrapped.IsWrapped = wrapped;
            _currentWrappedSet.Value = true;
        }
    }
}

namespace Hubcon.Shared.Core.Context
{
    /// <summary>
    /// The wrapped envelope class.
    /// </summary>
    public sealed class WrappedEnvelope
    {
        /// <summary>
        /// Determines if the response is set.
        /// </summary>
        public bool ResponseIsSet => Response != null;
        private object? Response { get; set; }

        /// <summary>
        /// Determines if the call is wrapped.
        /// </summary>
        public bool IsWrapped { get; internal set; }

        /// <summary>
        /// Determines if the pipeline should check auth refresh before proceeding.
        /// </summary>
        public bool ShouldCheckAuth { get; internal set; }

        /// <summary>
        /// Sets the should check auth property.
        /// </summary>
        /// <param name="shouldCheckAuth"></param>
        public void SetShouldCheckAuth(bool shouldCheckAuth)
        {
            ShouldCheckAuth = shouldCheckAuth;
        }

        /// <summary>
        /// Sets the hubcon response.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="response"></param>
        public void SetResponse<T>(IHubconResponse<T> response)
        {
            Response = response;
        }

        /// <summary>
        /// Sets the hubcon response using an <see cref="IResponse"/>
        /// </summary>
        /// <param name="response"></param>
        public void SetResponse(IResponse response)
        {
            Response = response;
        }

        /// <summary>
        /// Gets the typed response.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public IHubconResponse<T> GetResponse<T>()
        {
            return Response as IHubconResponse<T> ?? HubconResponse.Fail<T>("Empty response");
        }

        /// <summary>
        /// Gets the generic response.
        /// </summary>
        /// <returns></returns>
        public IResponse GetResponse()
        {
            return Response as IResponse ?? HubconResponse.Fail("Empty response");
        }

        /// <summary>
        /// Gets the raw response object.
        /// </summary>
        /// <returns></returns>
        public object? GetRawResponse()
        {
            return Response;
        }
    }

    /// <summary>
    /// Manages the execution of interceptors and hooks during the lifecycle of a client-side operation.
    /// Coordinates between global client options, contract-specific settings, and individual operation configurations.
    /// </summary>
    public sealed class InterceptorManager : IInterceptorManager
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InterceptorManager"/> class.
        /// </summary>
        /// <param name="serviceProvider">The <see cref="IServiceProvider"/> used for dependency resolution.</param>
        /// <param name="clientOptions">The global <see cref="IClientOptions"/>.</param>
        /// <param name="contractOptions">The specific <see cref="IContractOptions"/> for the current contract.</param>
        /// <param name="operationOptions">Optional <see cref="IOperationOptions"/> for the specific method call.</param>
        /// <param name="context">Optional <see cref="IInvocationContext"/> representing the current execution state.</param>
        public InterceptorManager(IServiceProvider serviceProvider, IClientOptions clientOptions, IContractOptions contractOptions, IOperationOptions? operationOptions = null, IInvocationContext? context = null)
        {
            ServiceProvider = serviceProvider;
            ClientOptions = clientOptions;
            ContractOptions = contractOptions;
            OperationOptions = operationOptions;
            Request = context?.Request;
            _context = context!;
        }

        /// <summary>
        /// Gets the service provider.
        /// </summary>
        public IServiceProvider ServiceProvider { get; }

        /// <summary>
        /// Gets the global client configuration options.
        /// </summary>
        public IClientOptions ClientOptions { get; }

        /// <summary>
        /// Gets the configuration options for the current contract.
        /// </summary>
        public IContractOptions ContractOptions { get; }

        /// <summary>
        /// Gets the configuration options for the specific operation, if available.
        /// </summary>
        public IOperationOptions? OperationOptions { get; }

        /// <summary>
        /// Gets the current operation request, if available.
        /// </summary>
        public IOperationRequest? Request { get; }

        private IInvocationContext? _context;

        /// <summary>
        /// Retrieves the current execution context or creates a new one if none exists.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token to associate with the context.</param>
        /// <returns>An instance of <see cref="IInvocationContext"/>.</returns>
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

        /// <summary>
        /// Asynchronously triggers the validation hooks defined for the current operation.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task CallValidationHooks(CancellationToken cancellationToken = default)
        {
            if (OperationOptions == null)
                return;

            await OperationOptions.CallValidationHook(ServiceProvider, Request!, cancellationToken);
        }

        /// <summary>
        /// Asynchronously triggers a specific type of interceptor defined in the client options.
        /// </summary>
        /// <param name="interceptorType">The <see cref="InterceptorType"/> to execute.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task CallInterceptor(InterceptorType interceptorType, CancellationToken cancellationToken = default)
        {
            var context = GetContext(cancellationToken);
            await ClientOptions.CallInterceptor(interceptorType, context);
        }

        /// <summary>
        /// Asynchronously triggers hooks at both the operation and contract levels.
        /// </summary>
        /// <param name="hookType">The <see cref="HookType"/> to execute.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task CallHooks(HookType hookType, CancellationToken cancellationToken = default)
        {
            var context = GetContext(cancellationToken);
            if (OperationOptions != null) await OperationOptions.CallHook(hookType, context);
            await ContractOptions.CallHook(hookType, context);
        }

        /// <summary>
        /// Asynchronously triggers hooks and attempts to execute matching interceptors based on the hook type.
        /// </summary>
        /// <param name="hookType">The <see cref="HookType"/> used to resolve hooks and interceptors.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
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

    /// <summary>
    /// Represents the execution context for a single client-side call or invocation.
    /// Stores state, request data, and results for the duration of the call lifecycle.
    /// </summary>
    public sealed class CallContext : IInvocationContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CallContext"/> class.
        /// </summary>
        /// <param name="services">The service provider for dependency resolution.</param>
        /// <param name="request">The <see cref="IOperationRequest"/> associated with this call.</param>
        /// <param name="authenticationManager">The manager responsible for call authentication.</param>
        /// <param name="isWrapped">Indicates if the call is wrapped in a specific execution logic.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> for the operation.</param>
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
            Logger = Services.GetRequiredService<ILogger<IHubconClient>>();
        }

        /// <summary>
        /// Gets the service provider.
        /// </summary>
        public IServiceProvider Services { get; private set; }

        /// <summary>
        /// Gets the request details for this invocation.
        /// </summary>
        public IOperationRequest Request { get; }

        /// <summary>
        /// Gets the authentication manager assigned to this context.
        /// </summary>
        public IAuthenticationManager AuthenticationManager { get; }

        /// <summary>
        /// Gets or sets the cancellation token for the current operation.
        /// </summary>
        public CancellationToken CancellationToken { get; internal set; }

        /// <summary>
        /// Gets a value indicating whether the invocation context is wrapped.
        /// </summary>
        public bool IsWrapped { get; private set; }

        /// <summary>
        /// Gets a value indicating whether an exception has occurred during execution.
        /// </summary>
        public bool HasError => Exception != null;

        /// <summary>
        /// Gets the exception that occurred during execution, if any.
        /// </summary>
        public Exception? Exception { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating whether the operation was successful.
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Gets or sets the HTTP or protocol-specific status code for the operation.
        /// </summary>
        public int StatusCode { get; set; }

        /// <summary>
        /// Gets or sets the logger used to record diagnostic information for this context.
        /// </summary>
        public ILogger Logger { get; set; }

        /// <summary>
        /// Records an exception in the context and marks the operation as unsuccessful.
        /// </summary>
        /// <param name="ex">The <see cref="Exception"/> to record.</param>
        public void SetException(Exception ex)
        {
            Exception ??= ex;
            IsSuccess = false;
        }
    }
}
