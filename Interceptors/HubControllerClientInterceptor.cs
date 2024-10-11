using Castle.DynamicProxy;
using Hubcon.Extensions;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Interceptors
{
    internal class HubControllerClientInterceptor : AsyncInterceptorBase
    {
        private protected string _targetClientId { get; private set; }
        private protected IHubContext<Hub> _hub { get; private set; }

        public HubControllerClientInterceptor(IHubContext<Hub> hub, string targetClientId)
        {
            _hub = hub;
            _targetClientId = targetClientId;
        }

        protected override async Task<TResult> InterceptAsync<TResult>(IInvocation invocation, IInvocationProceedInfo proceedInfo, Func<IInvocation, IInvocationProceedInfo, Task<TResult>> proceed)
        {
            // Interceptando el método asíncrono con resultado
            Console.WriteLine($"Interceptando método asíncrono con resultado: {invocation.Method.Name}");

            // Lógica antes de llamar al método original
            var result = await _hub.Clients
                .Client(_targetClientId)
                .InvokeMethodAsync(invocation.Method.Name, new CancellationToken(), invocation.Arguments);

            // Convertir el resultado y devolverlo
            TResult convertedResult = JsonElementTools.JsonElementParser.ConvertJsonElement<TResult>(result.Data!);
            return convertedResult;
        }

        protected override async Task InterceptAsync(IInvocation invocation, IInvocationProceedInfo proceedInfo, Func<IInvocation, IInvocationProceedInfo, Task> proceed)
        {
            // Interceptando el método asíncrono
            Console.WriteLine($"Interceptando método asíncrono: {invocation.Method.Name}");

            // Lógica antes de llamar al método original
            await _hub.Clients
                .Client(_targetClientId)
                .CallMethodAsync(invocation.Method.Name, new CancellationToken(), invocation.Arguments);

            proceedInfo.Invoke();

            // Lógica después de llamar al método
            Console.WriteLine($"Después de llamar a: {invocation.Method.Name}");
        }
    }
}
