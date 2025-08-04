using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Abstractions.Standard.Extensions;
using Hubcon.Shared.Abstractions.Standard.Interfaces;
using Hubcon.Shared.Core.Cache;
using Hubcon.Shared.Core.Tools;
using Hubcon.Shared.Core.Websockets.Interfaces;
using System.Reflection;
using System.Runtime.CompilerServices;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async ValueTask<T> InvokeAsync<T>(MethodInfo method, Dictionary<string, object?> arguments, CancellationToken cancellationToken)
        {
            T result;

            var methodName = method.GetMethodSignature();
            var contractName = method.ReflectedType!.Name;
            var resultType = typeof(T);

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

                using var cts = new CancellationTokenSource();
                using var registration = cancellationToken.Register(cts.Cancel);

                IAsyncEnumerable<JsonElement> stream = Client.GetStream(request, method, cts.Token);
                result = (T)streamMethod.Invoke(converter, [stream, cts.Token])!;
            }
            else if (_hasAsyncEnumerablesCache.GetOrAdd(
                method,
                x => method.GetParameters().Any(x => x.ParameterType.IsGenericType && x.ParameterType.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>))))
            {
                var request = new OperationRequest(methodName, contractName, arguments);
                return await Client!.Ingest<T>(request, method, cancellationToken);
            }
            else
            {
                OperationRequest request = new(
                    methodName,
                    contractName,
                    arguments
                );

                result = await Client!.SendAsync<T>(
                    request,
                    method,
                    cancellationToken
                );
            }

            return result!;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task CallAsync(MethodInfo method, Dictionary<string, object?> arguments, CancellationToken cancellationToken)
        {
            var methodName = method.GetMethodSignature();
            var contractName = method.ReflectedType!.Name;

            if (_hasAsyncEnumerablesCache.GetOrAdd(
                method,
                x => method.GetParameters().Any(x => x.ParameterType.IsGenericType && x.ParameterType.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>))))
            {
                using var cts = new CancellationTokenSource();
                using var register = cancellationToken.Register(cts.Cancel);

                var request = new OperationRequest(methodName, contractName, arguments);
                return Client!.Ingest<JsonElement>(request, method, cts.Token);
            }
            else
            {
                OperationRequest request = new(
                    methodName,
                    contractName,
                    arguments
                );

                return Client!.CallAsync(request, method, cancellationToken);
            }
        }
    }
}
