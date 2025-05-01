using Castle.DynamicProxy;
using Hubcon.Core.Converters;
using Hubcon.Core.Extensions;
using Hubcon.Core.Models;
using Hubcon.Core.Models.Interfaces;

namespace Hubcon.Core.Interceptors
{
    public class ClientControllerConnectorInterceptor : AsyncInterceptorBase, IClientControllerConnectorInterceptor
    {
        private readonly IDynamicConverter _converter;

        private Func<ICommunicationHandler> HandlerFactory { get; set; }

        public ICommunicationHandler CommunicationHandler { get => HandlerFactory.Invoke(); }

        public ClientControllerConnectorInterceptor(IHubconControllerManager hubconController, IDynamicConverter converter)
        {
            HandlerFactory = () => hubconController.CommunicationHandler;
            _converter = converter;
        }

        protected override async Task<TResult> InterceptAsync<TResult>(IInvocation invocation, IInvocationProceedInfo proceedInfo, Func<IInvocation, IInvocationProceedInfo, Task<TResult>> proceed)
        {      
            TResult? result;

            var handler = HandlerFactory.Invoke();
            var methodName = invocation.Method.GetMethodSignature();
            var contractName = invocation.Method.ReflectedType!.Name;
            var resultType = typeof(TResult);

            if (resultType.IsGenericType && resultType.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>))
            {
                var itemType = resultType.GetGenericArguments()[0];

                var streamMethod = handler
                    .GetType()
                    .GetMethod(nameof(handler.StreamAsync))!
                    .MakeGenericMethod(itemType);

                MethodInvokeRequest request = new(
                    methodName,
                    contractName,
                    _converter.SerializeArgsToJson(invocation.Arguments)
                );

                // Invocar el método StreamAsync pasando el tipo adecuado
                result = await (Task<TResult>)streamMethod.Invoke(handler, new object[]
                {
                    request,
                    invocation.Method,
                    new CancellationToken()
                })!;
            }
            else
            {
                MethodInvokeRequest request = new(
                    methodName,
                    contractName,
                    _converter.SerializeArgsToJson(invocation.Arguments)
                );

                var response = await handler.InvokeAsync(request, invocation.Method, new CancellationToken());
                result = _converter.DeserializeData<TResult>(response.Data);
            }

            invocation.ReturnValue = result;
            return result!;
        }

        protected override async Task InterceptAsync(IInvocation invocation, IInvocationProceedInfo proceedInfo, Func<IInvocation, IInvocationProceedInfo, Task> proceed)
        {
            var handler = HandlerFactory.Invoke();
            var methodName = invocation.Method.GetMethodSignature();
            var contractName = invocation.Method.ReflectedType!.Name;

            MethodInvokeRequest request = new(
                methodName,
                contractName,
                _converter.SerializeArgsToJson(invocation.Arguments)
            );

            await handler.CallAsync(request, invocation.Method, new CancellationToken());
        }
    }
}
