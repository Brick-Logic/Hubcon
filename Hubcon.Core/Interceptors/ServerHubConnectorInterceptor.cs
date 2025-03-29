using Castle.DynamicProxy;
using Hubcon.Core.Extensions;
using Hubcon.Core.Interfaces.Communication;


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
                var streamMethod = handler.GetType() // Cambia 'Handler' por el tipo adecuado
                    .GetMethod(nameof(handler.StreamAsync))! // Cambia 'StreamAsync' por el nombre correcto del método
                    .MakeGenericMethod(itemType);

                // Invocar el método StreamAsync pasando el tipo adecuado
                result = (TResult)streamMethod.Invoke(handler, new object[]
                {
                    invocation.Method.GetMethodSignature(),
                    invocation.Arguments,
                    new CancellationToken()
                })!;
            }
            else
            {
                var response = await handler.InvokeAsync(
                    invocation.Method.GetMethodSignature(),
                    invocation.Arguments,
                    new CancellationToken()
                );

                result = response.GetDeserializedData<TResult>()!;
            }


            // Convertir el resultado y devolverlo
            invocation.ReturnValue = result;
            return result;
        }

        protected override async Task InterceptAsync(IInvocation invocation, IInvocationProceedInfo proceedInfo, Func<IInvocation, IInvocationProceedInfo, Task> proceed)
        {
            Console.WriteLine($"[Client][MethodInterceptor] Calling {invocation.Method.Name} on SERVER. Args: [{string.Join(",", invocation.Arguments.Select(x => $"{x}"))}]");

            await handler.CallAsync(
                invocation.Method.GetMethodSignature(),
                invocation.Arguments,
                new CancellationToken()
            );
        }
    }
}
