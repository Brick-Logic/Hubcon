using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Abstractions.Standard.Extensions;
using Hubcon.Shared.Abstractions.Standard.Interfaces;
using Hubcon.Shared.Core.Cache;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;

namespace Hubcon.Client.Interceptors
{
    internal sealed class ClientProxyInterceptor(IDynamicConverter converter) : IClientProxyInterceptor
    {
        private readonly static ImmutableCache<Type, MethodInfo> _methodInfoCache = new();
        private readonly static ImmutableCache<MethodInfo, bool> _hasAsyncEnumerablesCache = new();
        private readonly static ImmutableCache<Type, bool> _isAsyncEnumerablesCache = new();

        IHubconClient? Client;

        public void InjectClient(IHubconClient client)
        {
            Client ??= client;
        }

        public async ValueTask<T> InvokeAsync<T>(MethodInfo method, Dictionary<string, object?>? arguments = null)
        {
            if (Client is null)
                throw new Exception("El cliente no fue inyectado.");

            T result;

            var methodName = method.GetMethodSignature();
            var contractName = method.ReflectedType!.Name;
            var resultType = typeof(T);
            using var cts = new CancellationTokenSource();

            if (_isAsyncEnumerablesCache.GetOrAdd(resultType, x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>)))
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

                result = (T)streamMethod.Invoke(converter, [stream, cts.Token])!;
            }
            else if (_hasAsyncEnumerablesCache.GetOrAdd(
                method,
                x => method.GetParameters().Any(x => x.ParameterType.IsGenericType && x.ParameterType.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>))))
            {
                var request = new OperationRequest(methodName, contractName, arguments);
                return await Client.Ingest<T>(request);
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
        {            if (Client is null)
                throw new Exception("El cliente no fue inyectado.");

            var tokens = arguments?.Where(x => x.Value?.GetType() == typeof(CancellationToken));
            
            if(tokens != null)
            {
                foreach(var token in tokens!)
                {
                    arguments!.Remove(token.Key);
                }
            }

            var methodName = method.GetMethodSignature();
            var contractName = method.ReflectedType!.Name;

            if (_hasAsyncEnumerablesCache.GetOrAdd(
                method, 
                x => method.GetParameters().Any(x => x.ParameterType.IsGenericType && x.ParameterType.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>))))
            {
                var request = new OperationRequest(methodName, contractName, arguments);
                return Client.Ingest<JsonElement>(request);
            }
            else
            {
                OperationRequest request = new(
                    methodName,
                    contractName,
                    arguments
                );

                return Client.CallAsync(request, method);
            }
        }
    }
}
