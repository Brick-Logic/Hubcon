using Castle.DynamicProxy;
using Hubcon.Extensions;
using Microsoft.AspNetCore.SignalR.Client;

namespace Hubcon.Interceptors
{
    internal class ServerHubConnectorInterceptor(HubConnection hub) : AsyncInterceptorBase
    {
        protected override async Task<TResult> InterceptAsync<TResult>(IInvocation invocation, IInvocationProceedInfo proceedInfo, Func<IInvocation, IInvocationProceedInfo, Task<TResult>> proceed)
        {
            if (hub.State != HubConnectionState.Connected) await hub.StartAsync();

            TResult? result = await hub.InvokeServerMethodAsync<TResult?>(invocation.Method.GetMethodSignature(), new CancellationToken(), invocation.Arguments);

            // Convertir el resultado y devolverlo
            invocation.ReturnValue = result;
            return result!;
        }

        protected override async Task InterceptAsync(IInvocation invocation, IInvocationProceedInfo proceedInfo, Func<IInvocation, IInvocationProceedInfo, Task> proceed)
        {
            if (hub.State != HubConnectionState.Connected) await hub.StartAsync();
            await hub.CallServerMethodAsync(invocation.Method.GetMethodSignature(), new CancellationToken(), invocation.Arguments);
        }
    }
}
