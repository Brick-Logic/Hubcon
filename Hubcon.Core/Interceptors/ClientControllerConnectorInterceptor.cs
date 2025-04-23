using Castle.DynamicProxy;
using Hubcon.Core.Converters;
using Hubcon.Core.Extensions;
using Hubcon.Core.Models;
using Hubcon.Core.Models.Interfaces;

namespace Hubcon.Core.Interceptors
{
    public class ClientControllerConnectorInterceptor<TIHubController, TICommunicationHandler> : AsyncInterceptorBase
        where TICommunicationHandler : ICommunicationHandler
        where TIHubController : IBaseHubconController
    {
        private readonly DynamicConverter _converter;

        private Func<ICommunicationHandler> HandlerFactory { get; set; }

        public ICommunicationHandler CommunicationHandler { get => HandlerFactory.Invoke(); }

        public ClientControllerConnectorInterceptor(TIHubController handler, DynamicConverter converter)
        {
            HandlerFactory = () => handler.HubconController.CommunicationHandler;
            _converter = converter;
        }

        protected override async Task<TResult> InterceptAsync<TResult>(IInvocation invocation, IInvocationProceedInfo proceedInfo, Func<IInvocation, IInvocationProceedInfo, Task<TResult>> proceed)
        {
            
            var handler = HandlerFactory.Invoke();
            TResult? result;

            if (typeof(TResult).IsGenericType && typeof(TResult).GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>))
            {
                // Obtener el tipo de los elementos de IAsyncEnumerable<T>
                var itemType = typeof(TResult).GetGenericArguments()[0];

                // Crear el método adecuado que se espera
                var streamMethod = handler
                    .GetType() // Cambia 'Handler' por el tipo adecuado
                    .GetMethod(nameof(handler.StreamAsync))! // Cambia 'StreamAsync' por el nombre correcto del método
                    .MakeGenericMethod(itemType);

                var methodName = invocation.Method.GetMethodSignature();

                MethodInvokeRequest request = new MethodInvokeRequest(
                    methodName,
                    methodName,
                    _converter.SerializeArgsToJson(invocation.Arguments)
                );

                // Invocar el método StreamAsync pasando el tipo adecuado
                result = await (Task<TResult>)streamMethod.Invoke(handler, new object[]
                {
                    request,
                    new CancellationToken()
                })!;
            }
            else
            {
                var methodName = invocation.Method.GetMethodSignature();

                MethodInvokeRequest request = new MethodInvokeRequest(
                    methodName,
                    methodName,
                    _converter.SerializeArgsToJson(invocation.Arguments)
                );

                var response = await handler.InvokeAsync(request, new CancellationToken());
                result = _converter.DeserializeData<TResult>(response.Data);
            }

            invocation.ReturnValue = result;
            return result!;
        }

        protected override async Task InterceptAsync(IInvocation invocation, IInvocationProceedInfo proceedInfo, Func<IInvocation, IInvocationProceedInfo, Task> proceed)
        {
            var handler = HandlerFactory.Invoke();

            var methodName = invocation.Method.GetMethodSignature();

            MethodInvokeRequest request = new MethodInvokeRequest(
                methodName,
                methodName,
                _converter.SerializeArgsToJson(invocation.Arguments)
            );

            await handler.CallAsync(request, new CancellationToken());
        }
    }
}
