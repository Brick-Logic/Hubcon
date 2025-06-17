using Castle.DynamicProxy;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Abstractions.Standard.Interfaces;
using Hubcon.Shared.Core.Extensions;
using Hubcon.Shared.Core.Tools;
using Hubcon.Shared.Entrypoint;
using Hubcon.Shared.Abstractions.Standard.Extensions;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Text.Json;

namespace Hubcon.Client.Interceptors
{
    public class ClientProxyInterceptor(
        IHubconClient client,
        IDynamicConverter converter,
        ILogger<ClientProxyInterceptor> logger) : IClientProxyInterceptor
    {
        public IHubconClient Client => client;

        public async Task<T> InvokeAsync<T>(MethodInfo method, params object[] arguments)
        {
            T result;

            var methodName = method.GetMethodSignature();
            var contractName = method.ReflectedType!.Name;
            var resultType = typeof(T);
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
                    converter.SerializeArgsToJson(arguments)
                );

                IAsyncEnumerable<JsonElement> stream = Client.GetStream(request, cts.Token);

                result = (T)streamMethod.Invoke(converter, new object[]
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
                    converter.SerializeArgsToJson(arguments)
                );

                result = await Client.SendAsync<T>(
                    request,
                    method,
                    cts.Token
                );

            }

            return result!;
        }

        public Task CallAsync(MethodInfo method, params object[] arguments)
        {
            var methodName = method.GetMethodSignature();
            var contractName = method.ReflectedType!.Name;
            using var cts = new CancellationTokenSource();

            if (arguments.Length == 0 && arguments.Any(EnumerableTools.IsAsyncEnumerable))
            {
                OperationRequest request = new(methodName, contractName, null);

                return Client.Ingest(request, arguments);
            }
            else
            {
                OperationRequest request = new(
                    methodName,
                    contractName,
                    converter.SerializeArgsToJson(arguments)
                );

                return Client.CallAsync(request, method, cts.Token);
            }
        }
    }
}
