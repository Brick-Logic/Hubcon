using Castle.DynamicProxy;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Core.Extensions;

namespace Hubcon.Client.Interceptors
{
    public class ServerConnectorInterceptor : AsyncInterceptorBase, IContractInterceptor
    {
        public ICommunicationHandler CommunicationHandler { get; }
        private readonly IDynamicConverter _converter;

        public ServerConnectorInterceptor(ICommunicationHandler handler, IDynamicConverter converter)
        {
            CommunicationHandler = handler;
            _converter = converter;
        }

        protected override async Task<TResult> InterceptAsync<TResult>(IInvocation invocation, IInvocationProceedInfo proceedInfo, Func<IInvocation, IInvocationProceedInfo, Task<TResult>> proceed)
        {
            TResult result;

            if (typeof(TResult).IsGenericType && typeof(TResult).GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>))
            {
                // Obtener el tipo de los elementos de IAsyncEnumerable<T>
                var itemType = typeof(TResult).GetGenericArguments()[0];

                // Crear el método adecuado que se espera
                var streamMethod = CommunicationHandler
                    .GetType() // Cambia 'Handler' por el tipo adecuado
                    .GetMethod(nameof(CommunicationHandler.StreamAsync))! // Cambia 'StreamAsync' por el nombre correcto del método
                    .MakeGenericMethod(itemType);

                var request = new OperationRequest(
                    invocation.Method.GetMethodSignature(),
                    invocation.Method.ReflectedType!.Name,
                    _converter.SerializeArgsToJson(invocation.Arguments)
                );

                // Invocar el método StreamAsync pasando el tipo adecuado
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
                result = _converter.DeserializeObject<TResult>(response.Data!)!;
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
