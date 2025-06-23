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
using System.Collections.Concurrent;

namespace Hubcon.Client.Interceptors
{
    public class ClientProxyInterceptor(
        IHubconClient client,
        IDynamicConverter converter,
        ILogger<ClientProxyInterceptor> logger) : IClientProxyInterceptor
    {
        public IHubconClient Client => client;

        private static ConcurrentDictionary<Type, MethodInfo> _methodInfoCache = new();
        private static ConcurrentDictionary<MethodInfo, bool> _hasAsyncEnumerablesCache = new();

        public async Task<T> InvokeAsync<T>(MethodInfo method, Dictionary<string, object?>? arguments = null)
        {
            T result;

            var methodName = method.GetMethodSignature();
            var contractName = method.ReflectedType!.Name;
            var resultType = typeof(T);
            logger.LogInformation(resultType.FullName);
            using var cts = new CancellationTokenSource();

            if (resultType.IsGenericType && resultType.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>))
            {
                var streamMethod = _methodInfoCache.GetOrAdd(
                    resultType.GetGenericArguments()[0],
                    x => typeof(IDynamicConverter).GetMethod(nameof(converter.ConvertStream))!.MakeGenericMethod(x));


                OperationRequest request = new(
                    methodName,
                    contractName,
                    arguments
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
                    arguments
                );

                result = await Client.SendAsync<T>(
                    request,
                    method,
                    cts.Token
                );

            }

            return result!;
        }

        public Task CallAsync(MethodInfo method, Dictionary<string, object?>? arguments = null)
        {
            var methodName = method.GetMethodSignature();
            var contractName = method.ReflectedType!.Name;
            using var cts = new CancellationTokenSource();


            if (_hasAsyncEnumerablesCache.GetOrAdd(
                method, 
                x => method.GetParameters().Any(x => x.ParameterType.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>))))
            {
                var request = new OperationRequest(methodName, contractName, null);
                return Client.Ingest(request, arguments);
            }
            else
            {
                OperationRequest request = new(
                    methodName,
                    contractName,
                    arguments
                );

                return Client.CallAsync(request, method, cts.Token);
            }
        }
    }
}
