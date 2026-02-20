using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Client.Core.Configurations;
using Hubcon.Client.Core.Exceptions;
using Hubcon.Client.Core.HubconInvocationContext;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Abstractions.Standard.Extensions;
using Hubcon.Shared.Abstractions.Standard.Interceptor;
using Hubcon.Shared.Core.Context;
using Hubcon.Shared.Core.Extensions;
using Hubcon.Shared.Core.Tools;
using Hubcon.Shared.Core.Websockets.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Hubcon.Client.Core.Proxies
{
    public interface IContractDataAccessor
    {
        IAuthenticationManager AuthenticationManager { get; }

        ITransportClient GetTransportClient<T>() where T : HubconTransportAttribute;
    }

    public abstract class BaseContractProxy : BaseClientProxyMarker, IContractDataAccessor
    {
        private Dictionary<Type, ITransportClient> _transports;
        private IImmutableDictionary<string, IClientOperationContext> _operations = null!;
        private string SimpleContractName { get; set; } = string.Empty;

        private Type _contractType = null!;
        private IHubconClient _client = null!;
        private IDynamicConverter _converter = null!;
        private IClientOptions _clientOptions = null!;
        private IServiceProvider rootServiceProvider = null!;
        private IServiceScope serviceScope = null!;

        public abstract void SetPropertyValue(string propertyName, object value);

        public IImmutableDictionary<string, IClientOperationContext> BuildContractProxy(
            IHubconClient client,
            IClientOptions clientOptions,
            IServiceScope serviceScope,
            ConcurrentDictionary<Type, IContractOptions> contractOptionsDict,
            IDynamicConverter converter)
        {
            _client = client;
            _converter = converter;
            _clientOptions = clientOptions;
            rootServiceProvider = serviceScope.ServiceProvider;
            this.serviceScope = serviceScope;

            Dictionary<string, IClientOperationContext> tempOperations = new();

            _contractType = GetType()
                .GetInterfaces()
                .First(x => typeof(IControllerContract).IsAssignableFrom(x) && x != typeof(IControllerContract));

            var methods = _contractType
                .GetMethods()
                .Where(m => !m.IsSpecialName);

            SimpleContractName = NamingHelper.GetCleanName(_contractType.Name);

            var env = Environment.GetEnvironmentVariable("HUBCON_OPNAME_DEBUG_ENABLED");
            var useHashed = !bool.TryParse(env, out var parsed) ? true : !parsed;

            IContractOptions contractOptions = clientOptions.GetContractOptions(_contractType);
            var interceptorManager = new InterceptorManager(rootServiceProvider, clientOptions, contractOptions, null, null);

            Dictionary<Type, ITransportClient> transports = new();

            foreach (var method in methods)
            {
                var signature = method.GetMethodSignature(false);
                IClientOperationContext context = new ClientOperationContext(method, interceptorManager, rootServiceProvider, clientOptions, contractOptions, _contractType, transports);
                tempOperations.Add(signature, context);
            }

            //foreach (var prop in _contractType.GetProperties().Where(x => x.PropertyType.IsGenericType && x.PropertyType.GetGenericTypeDefinition() == typeof(ISubscription<>)))
            //{
            //    IClientOperationContext context = new ClientOperationContext(prop, interceptorManager, rootServiceProvider, clientOptions, contractOptions, _contractType, transports);
            //    tempOperations.Add(prop.Name, context);
            //}

            _transports = transports;
            _operations = tempOperations.ToImmutableDictionary();
            return _operations;
        }

        public async Task<T> InvokeAsync<T>(string methodSignature, Dictionary<string, object> arguments, CancellationToken cancellationToken)
        {
            if (_operations.TryGetValue(methodSignature, out IClientOperationContext? context))
            {
                OperationRequest request = new OperationRequest(context.MethodSignature, SimpleContractName, arguments!);
                _ = WrappedContext.CurrentWrapped;
                var wrapped = WrappedContext.Current;

                using var scope = rootServiceProvider.CreateScope();
                var callContext = new CallContext(scope.ServiceProvider, request, AuthenticationManager, wrapped, cancellationToken);
                HubconContext.UseContext(callContext);

                var interceptorContext = new InterceptorManager(
                    scope.ServiceProvider,
                    context.ClientOptions,
                    context.ContractOptions,
                    context.OperationOptions,
                    callContext);

                InterceptorContext.UseContext(interceptorContext);

                await _client.SendAsync<T>(request, context, cancellationToken);

                var rawResponse = WrappedContext.CurrentWrapped.GetRawResponse();
                if (rawResponse is HubconResponse<T> hubconResponse)
                {
                    return hubconResponse.Data;
                }
                else if (rawResponse is T response)
                {
                    return response;
                }

                return default!;
            }
            else
            {
                throw new Exception($"Could not find the operation '{methodSignature}'.");
            }
        }

        public async Task CallAsync(string methodSignature, Dictionary<string, object> arguments, CancellationToken cancellationToken)
        {
            if (_operations.TryGetValue(methodSignature, out IClientOperationContext? context))
            {
                OperationRequest request = new OperationRequest(context.MethodSignature, SimpleContractName, arguments!);
                _ = WrappedContext.CurrentWrapped;
                var wrapped = WrappedContext.Current;

                using var scope = rootServiceProvider.CreateScope();
                var callContext = new CallContext(scope.ServiceProvider, request, AuthenticationManager, wrapped, cancellationToken);
                HubconContext.UseContext(callContext);

                var interceptorContext = new InterceptorManager(
                    scope.ServiceProvider,
                    context.ClientOptions,
                    context.ContractOptions,
                    context.OperationOptions,
                    callContext);

                InterceptorContext.UseContext(interceptorContext);

                await _client.CallAsync(request, context, cancellationToken);
            }
            else
            {
                throw new Exception($"Could not find the operation '{methodSignature}'.");
            }
        }

        public async Task<T> IngestAsync<T>(string methodSignature, Dictionary<string, object> arguments, CancellationToken cancellationToken)
        {
            if (_operations.TryGetValue(methodSignature, out IClientOperationContext? context))
            {
                OperationRequest request = new OperationRequest(context.MethodSignature, SimpleContractName, arguments!);
                _ = WrappedContext.CurrentWrapped;
                var wrapped = WrappedContext.Current;

                using var scope = rootServiceProvider.CreateScope();
                var callContext = new CallContext(scope.ServiceProvider, request, AuthenticationManager, wrapped, cancellationToken);
                HubconContext.UseContext(callContext);

                var interceptorContext = new InterceptorManager(
                    scope.ServiceProvider,
                    context.ClientOptions,
                    context.ContractOptions,
                    context.OperationOptions,
                    callContext);

                InterceptorContext.UseContext(interceptorContext);

                await _client.Ingest<T>(request, context, cancellationToken);

                var rawResponse = WrappedContext.CurrentWrapped.GetRawResponse();
                if (rawResponse is HubconResponse<T> hubconResponse)
                {
                    return hubconResponse.Data;
                }
                else if (rawResponse is T response)
                {
                    return response;
                }

                return default!;
            }
            else
            {
                throw new Exception($"Could not find the operation '{methodSignature}'.");
            }
        }

        public async Task IngestAsync(string methodSignature, Dictionary<string, object> arguments, CancellationToken cancellationToken)
        {
            if (_operations.TryGetValue(methodSignature, out IClientOperationContext? context))
            {
                OperationRequest request = new OperationRequest(context.MethodSignature, SimpleContractName, arguments!);
                _ = WrappedContext.CurrentWrapped;
                var wrapped = WrappedContext.Current;

                using var scope = rootServiceProvider.CreateScope();
                var callContext = new CallContext(scope.ServiceProvider, request, AuthenticationManager, wrapped, cancellationToken);
                HubconContext.UseContext(callContext);

                var interceptorContext = new InterceptorManager(
                    scope.ServiceProvider,
                    context.ClientOptions,
                    context.ContractOptions,
                    context.OperationOptions,
                    callContext);

                InterceptorContext.UseContext(interceptorContext);

                await _client.Ingest<JsonElement>(request, context, cancellationToken);
            }
            else
            {
                throw new Exception($"Could not find the operation '{methodSignature}'.");
            }

        }

        public IAsyncEnumerable<T> StreamAsync<T>(string methodSignature, Dictionary<string, object> arguments, CancellationToken cancellationToken)
        {
            if (_operations.TryGetValue(methodSignature, out IClientOperationContext? context))
            {
                OperationRequest request = new OperationRequest(context.MethodSignature, SimpleContractName, arguments!);
                _ = WrappedContext.CurrentWrapped;
                var wrapped = WrappedContext.Current;

                using var scope = rootServiceProvider.CreateScope();
                var callContext = new CallContext(scope.ServiceProvider, request, AuthenticationManager, wrapped, cancellationToken);
                HubconContext.UseContext(callContext);

                var interceptorContext = new InterceptorManager(
                    scope.ServiceProvider,
                    context.ClientOptions,
                    context.ContractOptions,
                    context.OperationOptions,
                    callContext);

                InterceptorContext.UseContext(interceptorContext);

                IAsyncEnumerable<JsonElement> stream = _client.GetStream(request, context, cancellationToken).Result;

                var receivedResponse = WrappedContext.CurrentWrapped.GetResponse<IAsyncEnumerable<JsonElement>>();

                var response = new HubconResponse<IAsyncEnumerable<T>?>(
                    receivedResponse.Success,
                    !receivedResponse.Success,
                    receivedResponse.Message,
                    receivedResponse.Error,
                    receivedResponse.StatusCode,
                    receivedResponse.Data == null ? default : ConvertStream<T>(stream, context, cancellationToken),
                    null
                );

                context.SetResponse(response);
                return (response.Data ?? default)!;
            }
            else
            {
                throw new Exception($"Could not find the operation '{methodSignature}'.");
            }
        }

        public async IAsyncEnumerable<T> ConvertStream<T>(IAsyncEnumerable<JsonElement> stream, IClientOperationContext context, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var enumerator = stream.GetAsyncEnumerator(cancellationToken);
            T? item;
            while (true)
            {
                try
                {
                    if (!await enumerator.MoveNextAsync() || cancellationToken.IsCancellationRequested)
                        break;

                    await context.AcquireRateLimiter();

                    item = context.Converter.DeserializeJsonElement<T>(enumerator.Current)!;
                }
                catch (Exception ex)
                {
                    await context.CallHooksAndInterceptors(HookType.OnError, cancellationToken);

                    if (HubconContext.Current?.IsWrapped == true)
                    {
                        HubconContext.Current.SetException(ex);
                        await context.SetResponse(HubconResponse.InternalError<IAsyncEnumerable<JsonElement>>(ex));
                    }

                    break;
                }

                await context.CallHooksAndInterceptors(HookType.OnEventReceived, cancellationToken);
                yield return item;
            }

            await context.CallHooksAndInterceptors(HookType.OnUnsubscribed, cancellationToken);
        }

        public ITransportClient GetTransportClient<T>() where T : HubconTransportAttribute
        {
            if(_transports.TryGetValue(typeof(T), out var value))
            {
                return value;
            }
            else
            {
                return default!;
            }
        }

        public IAuthenticationManager? AuthenticationManager => _clientOptions.AuthenticationManagerFactory?.GetValue<IAuthenticationManager>(rootServiceProvider);
    }
}
