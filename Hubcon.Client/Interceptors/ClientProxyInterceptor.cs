using Castle.DynamicProxy;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Core.Extensions;
using Hubcon.Shared.Core.Tools;
using Hubcon.Shared.Entrypoint;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Hubcon.Client.Interceptors
{
    public class ClientProxyInterceptor(
        IHubconClient client,
        IDynamicConverter converter,
        ILogger<ClientProxyInterceptor> logger) : AsyncInterceptorBase, IContractInterceptor
    {
        public IHubconClient Client => client;

        protected override async Task<TResult> InterceptAsync<TResult>(IInvocation invocation, IInvocationProceedInfo proceedInfo, Func<IInvocation, IInvocationProceedInfo, Task<TResult>> proceed)
        {
            TResult? result;

            var methodName = invocation.Method.GetMethodSignature();
            var contractName = invocation.Method.ReflectedType!.Name;
            var resultType = typeof(TResult);
            logger.LogInformation(resultType.FullName);
            using var cts = new CancellationTokenSource();

            if (resultType.IsGenericType && resultType.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>))
            {
                var itemType = resultType.GetGenericArguments()[0];

                var streamMethod = converter
                    .GetType()
                    .GetMethod(nameof(converter.ConvertStream))!
                    .MakeGenericMethod(itemType);

                OperationRequest request = new(
                    methodName,
                    contractName,
                    converter.SerializeArgsToJson(invocation.Arguments)
                );

                IAsyncEnumerable<JsonElement> stream = Client.GetStream(request, cts.Token);

                result = (TResult)streamMethod.Invoke(converter, new object[]
                {
                    stream,
                    cts.Token
                })!;
            }
            else
            {
                OperationRequest request = new(
                    methodName,
                    contractName,
                    converter.SerializeArgsToJson(invocation.Arguments)
                );

                var response = await Client.SendAsync(
                    request,
                    invocation.Method,
                    cts.Token
                );

                result = converter.DeserializeJsonElement<TResult>(response.Data);
            }

            invocation.ReturnValue = result;
            return result!;
        }

        protected override async Task InterceptAsync(IInvocation invocation, IInvocationProceedInfo proceedInfo, Func<IInvocation, IInvocationProceedInfo, Task> proceed)
        {
            var methodName = invocation.Method.GetMethodSignature();
            var contractName = invocation.Method.ReflectedType!.Name;
            using var cts = new CancellationTokenSource();

            if (invocation.Arguments.Any(EnumerableTools.IsAsyncEnumerable))
            {
                OperationRequest request = new(methodName, contractName, null);

                await Client.Ingest(request, invocation.Arguments);
            }
            else
            {
                OperationRequest request = new(
                    methodName,
                    contractName,
                    converter.SerializeArgsToJson(invocation.Arguments)
                );

                await Client.CallAsync(request, invocation.Method, cts.Token);
            }
        }
    }
}
