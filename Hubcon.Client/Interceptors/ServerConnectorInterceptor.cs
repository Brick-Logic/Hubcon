using Castle.Core.Internal;
using Castle.Core.Logging;
using Castle.DynamicProxy;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Abstractions.Standard.Interfaces;
using Hubcon.Shared.Core.Extensions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Hubcon.Client.Interceptors
{
    public class ServerConnectorInterceptor : AsyncInterceptorBase, IContractInterceptor
    {
        public ICommunicationHandler CommunicationHandler { get; }
        private readonly IDynamicConverter _converter;
        private readonly ILogger<ServerConnectorInterceptor> logger;

        public ServerConnectorInterceptor(ICommunicationHandler handler, IDynamicConverter converter, ILogger<ServerConnectorInterceptor> logger)
        {
            CommunicationHandler = handler;
            _converter = converter;
            this.logger = logger;
        }

        protected override async Task<TResult> InterceptAsync<TResult>(IInvocation invocation, IInvocationProceedInfo proceedInfo, Func<IInvocation, IInvocationProceedInfo, Task<TResult>> proceed)
        {
            TResult result;

            if (typeof(TResult).IsGenericType && typeof(TResult).GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>))
            {
                var itemType = typeof(TResult).GetGenericArguments()[0];

                var streamMethod = CommunicationHandler
                    .GetType() 
                    .GetMethod(nameof(CommunicationHandler.StreamAsync))!
                    .MakeGenericMethod(itemType);

                var request = new OperationRequest(
                    invocation.Method.GetMethodSignature(),
                    invocation.Method.ReflectedType!.Name,
                    _converter.SerializeArgsToJson(invocation.Arguments)
                );

                result = await (Task<TResult>)streamMethod.Invoke(CommunicationHandler, new object[]
                {
                    request,
                    invocation.Method,
                    new CancellationToken()
                })!;
            }
            else
            {
                OperationRequest request = new OperationRequest(
                    invocation.Method.GetMethodSignature(),
                    invocation.Method.ReflectedType!.Name,
                    _converter.SerializeArgsToJson(invocation.Arguments)
                );

                var response = await CommunicationHandler.InvokeAsync(request, invocation.Method, new CancellationToken());
                result = _converter.DeserializeJsonElement<TResult>(response.Data!)!;
            }


            // Convertir el resultado y devolverlo
            invocation.ReturnValue = result;
            return result;
        }

        protected override async Task InterceptAsync(IInvocation invocation, IInvocationProceedInfo proceedInfo, Func<IInvocation, IInvocationProceedInfo, Task> proceed)
        {
            OperationRequest request = new OperationRequest(
                invocation.Method.GetMethodSignature(),
                invocation.Method.ReflectedType!.Name,
                _converter.SerializeArgsToJson(invocation.Arguments)
            );

            await CommunicationHandler.CallAsync(request, invocation.Method, new CancellationToken());
        }
    }
}
