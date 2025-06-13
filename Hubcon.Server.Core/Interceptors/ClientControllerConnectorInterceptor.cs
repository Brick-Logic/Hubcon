using Castle.DynamicProxy;
using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Core.Extensions;
using Hubcon.Shared.Entrypoint;

namespace Hubcon.Server.Core.Interceptors
{
    //public class ClientControllerConnectorInterceptor(IHubconClient client, IDynamicConverter converter) : AsyncInterceptorBase, IClientControllerConnectorInterceptor
    //{
    //    protected override async Task<TResult> InterceptAsync<TResult>(IInvocation invocation, IInvocationProceedInfo proceedInfo, Func<IInvocation, IInvocationProceedInfo, Task<TResult>> proceed)
    //    {      
    //        TResult? result;

    //        var methodName = invocation.Method.GetMethodSignature();
    //        var contractName = invocation.Method.ReflectedType!.Name;
    //        Console.WriteLine(contractName);
    //        Console.WriteLine(invocation.Method);
    //        var resultType = typeof(TResult);
    //        var cts = new CancellationTokenSource();

    //        if (resultType.IsGenericType && resultType.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>))
    //        {
    //            var itemType = resultType.GetGenericArguments()[0];

    //            var streamMethod = converter
    //                .GetType()
    //                .GetMethod(nameof(converter.ConvertStream))!
    //                .MakeGenericMethod(itemType);

    //            OperationRequest request = new(
    //                methodName,
    //                contractName,
    //                converter.SerializeArgsToJson(invocation.Arguments)
    //            );

    //            var stream = client.GetStream(request, nameof(DefaultEntrypoint.HandleMethodStream), cts.Token);

    //            result = (TResult)streamMethod.Invoke(streamMethod, new object[]
    //            {
    //                stream,
    //                cts.Token
    //            })!;
    //        }
    //        else
    //        {
    //            OperationRequest request = new(
    //                methodName,
    //                contractName,
    //                converter.SerializeArgsToJson(invocation.Arguments)
    //            );

    //            var response = await client.SendRequestAsync(
    //                request, 
    //                invocation.Method, 
    //                nameof(DefaultEntrypoint.HandleMethodWithResult),
    //                cts.Token
    //            );

    //            result = converter.DeserializeData<TResult>(response.Data);
    //        }

    //        invocation.ReturnValue = result;
    //        return result!;
    //    }

    //    protected override async Task InterceptAsync(IInvocation invocation, IInvocationProceedInfo proceedInfo, Func<IInvocation, IInvocationProceedInfo, Task> proceed)
    //    {
    //        var methodName = invocation.Method.GetMethodSignature();
    //        var contractName = invocation.Method.ReflectedType!.Name;
    //        var cts = new CancellationTokenSource();

    //        OperationRequest request = new(
    //            methodName,
    //            contractName,
    //            converter.SerializeArgsToJson(invocation.Arguments)
    //        );

    //        await client.SendRequestAsync(request, invocation.Method, nameof(DefaultEntrypoint.HandleMethodVoid), cts.Token);
    //    }
    //}
}
