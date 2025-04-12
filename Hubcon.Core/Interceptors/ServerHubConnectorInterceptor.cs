using Castle.DynamicProxy;
using Hubcon.Core.Converters;
using Hubcon.Core.Extensions;
using Hubcon.Core.Models;
using Hubcon.Core.Models.Interfaces;
using System;
using System.Diagnostics.CodeAnalysis;


namespace Hubcon.Core.Interceptors
{
    public interface IServerConnectorInterceptor
    {

    }

    public class ServerConnectorInterceptor<TIHubController, TICommunicationHandler> : AsyncInterceptorBase
        where TICommunicationHandler : ICommunicationHandler
        where TIHubController : IBaseHubconController
    {
        public readonly ICommunicationHandler CommunicationHandler;
        private readonly DynamicConverter _converter;

        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ServerConnectorInterceptor<,>))]
        public ServerConnectorInterceptor(TIHubController handler, DynamicConverter converter)
        {
            CommunicationHandler = handler.HubconController.CommunicationHandler;
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

                var request = new MethodInvokeRequest(invocation.Method.GetMethodSignature(), nameof(IHubconServerController.HandleMethodStream), invocation.Arguments).SerializeArgs(_converter.SerializeArgs);

                // Invocar el método StreamAsync pasando el tipo adecuado
                result = await (Task<TResult>)streamMethod.Invoke(CommunicationHandler, new object[]
                {
                    request,
                    new CancellationToken()
                })!;
            }
            else
            {
                MethodInvokeRequest request = new MethodInvokeRequest(
                    invocation.Method.GetMethodSignature(),
                    nameof(IBaseHubconController.HandleMethodTask),
                    invocation.Arguments
                )
                .SerializeArgs(_converter.SerializeArgs);

                var response = await CommunicationHandler.InvokeAsync(request,new CancellationToken());
                result = response.GetDeserializedData<TResult>(_converter.DeserializeData<TResult>)!;
            }


            // Convertir el resultado y devolverlo
            invocation.ReturnValue = result;
            return result;
        }

        protected override async Task InterceptAsync(IInvocation invocation, IInvocationProceedInfo proceedInfo, Func<IInvocation, IInvocationProceedInfo, Task> proceed)
        {
            MethodInvokeRequest request = new MethodInvokeRequest(
                invocation.Method.GetMethodSignature(), 
                nameof(IBaseHubconController.HandleMethodVoid), 
                invocation.Arguments
            )
            .SerializeArgs(_converter.SerializeArgs);

            await CommunicationHandler.CallAsync(request,new CancellationToken());
        }
    }
}
