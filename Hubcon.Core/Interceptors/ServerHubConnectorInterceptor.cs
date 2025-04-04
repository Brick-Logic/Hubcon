using Castle.DynamicProxy;
using Hubcon.Core.Extensions;
using Hubcon.Core.Interfaces;
using Hubcon.Core.Interfaces.Communication;
using Hubcon.Core.Models;


namespace Hubcon.Core.Interceptors
{
    internal class ServerConnectorInterceptor : AsyncInterceptorBase
    {
        private readonly ICommunicationHandler handler;

        public ServerConnectorInterceptor(ICommunicationHandler handler)
        {
            this.handler = handler;
        }

        protected override async Task<TResult> InterceptAsync<TResult>(IInvocation invocation, IInvocationProceedInfo proceedInfo, Func<IInvocation, IInvocationProceedInfo, Task<TResult>> proceed)
        {
            Console.WriteLine($"[Client][MethodInterceptor] Calling {invocation.Method.Name} on SERVER. Args: [{string.Join(",", invocation.Arguments.Select(x => $"{x}"))}]");

            TResult result;

            if (typeof(TResult).IsGenericType && typeof(TResult).GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>))
            {
                // Obtener el tipo de los elementos de IAsyncEnumerable<T>
                var itemType = typeof(TResult).GetGenericArguments()[0];

                // Crear el método adecuado que se espera
                var streamMethod = handler
                    .GetType() // Cambia 'Handler' por el tipo adecuado
                    .GetMethod(nameof(handler.StreamAsync))! // Cambia 'StreamAsync' por el nombre correcto del método
                    .MakeGenericMethod(itemType);

                var request = new MethodInvokeRequest(invocation.Method.GetMethodSignature(), nameof(IHubconServerController.HandleMethodStream), invocation.Arguments).SerializeArgs();

                // Invocar el método StreamAsync pasando el tipo adecuado
                result = await (Task<TResult>)streamMethod.Invoke(handler, new object[]
                {
                    request,
                    new CancellationToken()
                })!;
            }
            else
            {
                MethodInvokeRequest request = new MethodInvokeRequest(
                    invocation.Method.GetMethodSignature(),
                    nameof(IHubconController.HandleMethodTask),
                    invocation.Arguments
                )
                .SerializeArgs();

                var response = await handler.InvokeAsync(request,new CancellationToken());
                result = response.GetDeserializedData<TResult>()!;
            }


            // Convertir el resultado y devolverlo
            invocation.ReturnValue = result;
            return result;
        }

        protected override async Task InterceptAsync(IInvocation invocation, IInvocationProceedInfo proceedInfo, Func<IInvocation, IInvocationProceedInfo, Task> proceed)
        {
            Console.WriteLine($"[Client][MethodInterceptor] Calling {invocation.Method.Name} on SERVER. Args: [{string.Join(",", invocation.Arguments.Select(x => $"{x}"))}]");

            MethodInvokeRequest request = new MethodInvokeRequest(
                invocation.Method.GetMethodSignature(), 
                nameof(IHubconController.HandleMethodVoid), 
                invocation.Arguments
            )
            .SerializeArgs();

            await handler.CallAsync(request,new CancellationToken());
        }
    }
}
