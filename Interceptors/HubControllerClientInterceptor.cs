using Castle.DynamicProxy;
using Hubcon.Extensions;
using Hubcon.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using System.ComponentModel;

namespace Hubcon.Interceptors
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal class ClientHubControllerConnectorInterceptor : AsyncInterceptorBase
    {
        private protected string TargetClientId { get; private set; }
        private protected IHubContext<Hub>? HubContext { get; private set; }
        private protected Hub? Hub { get; private set; }

        public ClientHubControllerConnectorInterceptor(IHubContext<Hub> hub, string clientId)
        {
            HubContext = hub;
            TargetClientId = clientId;
        }

        public ClientHubControllerConnectorInterceptor(Hub hub, string clientId)
        {
            Hub = hub;
            TargetClientId = clientId;
        }

        protected override async Task<TResult> InterceptAsync<TResult>(IInvocation invocation, IInvocationProceedInfo proceedInfo, Func<IInvocation, IInvocationProceedInfo, Task<TResult>> proceed)
        {
            // Lógica antes de llamar al método original
            TResult? result;

            if (Hub == null)
            {
                result = await HubContext!.Clients
                    .Client(TargetClientId)
                    .InvokeMethodAsync<TResult?>(invocation.Method.GetMethodSignature(), new CancellationToken(), invocation.Arguments);
            }
            else
            {
                result = await Hub.Clients
                    .Client(TargetClientId)
                    .InvokeMethodAsync<TResult?>(invocation.Method.GetMethodSignature(), new CancellationToken(), invocation.Arguments);

            }

            // Convertir el resultado y devolverlo
            invocation.ReturnValue = result!;
            return result!;
        }

        protected override async Task InterceptAsync(IInvocation invocation, IInvocationProceedInfo proceedInfo, Func<IInvocation, IInvocationProceedInfo, Task> proceed)
        {
            // Lógica antes de llamar al método original
            if (Hub == null)
            {
                await HubContext!.Clients
                    .Client(TargetClientId)
                    .CallMethodAsync(invocation.Method.GetMethodSignature(), new CancellationToken(), invocation.Arguments);
            }
            else
            {
                await Hub.Clients
                    .Client(TargetClientId)
                    .CallMethodAsync(invocation.Method.GetMethodSignature(), new CancellationToken(), invocation.Arguments);
            }
        }
    }
}
