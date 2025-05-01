using Castle.DynamicProxy;
using Hubcon.Core.Converters;
using Hubcon.Core.Extensions;
using Hubcon.Core.Models;
using Hubcon.Core.Models.Interfaces;
using System.Text.Json;


namespace Hubcon.Core.Interceptors
{
    public class ServerConnectorInterceptor<TICommunicationHandler> : AsyncInterceptorBase, IServerConnectorInterceptor<TICommunicationHandler>
        where TICommunicationHandler : ICommunicationHandler
    {
        public ICommunicationHandler CommunicationHandler { get; }
        private readonly IDynamicConverter _converter;

        public ServerConnectorInterceptor(TICommunicationHandler handler, IDynamicConverter converter)
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

                var request = new MethodInvokeRequest(
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
                MethodInvokeRequest request = new MethodInvokeRequest(
                    invocation.Method.GetMethodSignature(),
                    invocation.Method.ReflectedType!.Name,
                    _converter.SerializeArgsToJson(invocation.Arguments)
                );

                var response = await CommunicationHandler.InvokeAsync(request, invocation.Method, new CancellationToken());
                result = _converter.DeserializeJsonElement<TResult>((JsonElement)response.Data!)!;
            }


            // Convertir el resultado y devolverlo
            invocation.ReturnValue = result;
            return result;
        }

        protected override async Task InterceptAsync(IInvocation invocation, IInvocationProceedInfo proceedInfo, Func<IInvocation, IInvocationProceedInfo, Task> proceed)
        {
            MethodInvokeRequest request = new MethodInvokeRequest(
                invocation.Method.GetMethodSignature(),
                invocation.Method.ReflectedType!.Name,
                _converter.SerializeArgsToJson(invocation.Arguments)
            );

            await CommunicationHandler.CallAsync(request, invocation.Method, new CancellationToken());
        }
    }
}
