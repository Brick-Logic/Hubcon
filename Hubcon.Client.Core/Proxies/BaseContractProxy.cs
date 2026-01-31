using Hubcon.Client.Abstractions.Interfaces;
using Hubcon.Client.Core.Exceptions;
using Hubcon.Client.Core.HubconInvocationContext;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Abstractions.Standard.Cache;
using Hubcon.Shared.Abstractions.Standard.Extensions;
using Hubcon.Shared.Abstractions.Standard.Interceptor;
using Hubcon.Shared.Core.Context;
using Hubcon.Shared.Core.Extensions;
using Hubcon.Shared.Core.Tools;
using Hubcon.Shared.Core.Websockets.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
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
    }

    public abstract class BaseContractProxy : BaseProxy, IContractDataAccessor
    {
        private FrozenDictionary<string, IClientOperationContext> _operations = null!;
        private string SimpleContractName { get; set; } = string.Empty;

        private Type _contractType = null!;
        private IHubconClient _client = null!;
        private IDynamicConverter _converter = null!;
        private IClientOptions _clientOptions = null!;
        private IServiceProvider rootServiceProvider = null!;

        public abstract void SetPropertyValue(string propertyName, object value);

        public FrozenDictionary<string, IClientOperationContext> BuildContractProxy(
            IHubconClient client, 
            IClientOptions clientOptions, 
            IServiceProvider serviceProvider, 
            ConcurrentDictionary<Type, IContractOptions> contractOptionsDict, 
            IDynamicConverter converter)
        {
            _client = client;
            _converter = converter;
            _clientOptions = clientOptions;
            rootServiceProvider = serviceProvider;

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

            foreach (var method in methods)
            {
                var signature = method.GetMethodSignature(false);
                IContractOptions contractOptions = clientOptions.GetContractOptions(_contractType);
                IClientOperationContext context = new ClientOperationContext(method, rootServiceProvider, clientOptions, contractOptions, _contractType);
                tempOperations.Add(signature, context);
            }

            foreach (var prop in _contractType.GetProperties().Where(x => x.PropertyType.IsGenericType && x.PropertyType.GetGenericTypeDefinition() == typeof(ISubscription<>)))
            {
                IContractOptions contractOptions = clientOptions.GetContractOptions(_contractType);
                IClientOperationContext context = new ClientOperationContext(prop, rootServiceProvider, clientOptions, contractOptions, _contractType);
                tempOperations.Add(prop.Name, context);
            }

            _operations = tempOperations.ToFrozenDictionary();
            return _operations;
        }

        public override Task<T> InvokeAsync<T>(string methodSignature, Dictionary<string, object> arguments, CancellationToken cancellationToken)
        {
            if(_operations.TryGetValue(methodSignature, out IClientOperationContext? context))
            {
                OperationRequest request = new OperationRequest(context.MethodSignature, SimpleContractName, arguments!);
                var wrapped = WrappedContext.Current;

                using var scope = rootServiceProvider.CreateScope();
                var callContext = new CallContext(scope.ServiceProvider, request, AuthenticationManager, wrapped, cancellationToken);
                HubconContext.UseContext(callContext);
                return _client.SendAsync<T>(request, context, cancellationToken);
            }
            else
            {
                throw new Exception($"Could not find the operation '{methodSignature}'.");
            }
        }

        public override Task CallAsync(string methodSignature, Dictionary<string, object> arguments, CancellationToken cancellationToken)
        {
            if (_operations.TryGetValue(methodSignature, out IClientOperationContext? context))
            {
                OperationRequest request = new OperationRequest(context.MethodSignature, SimpleContractName, arguments!);
                var wrapped = WrappedContext.Current;

                using var scope = rootServiceProvider.CreateScope();
                var callContext = new CallContext(scope.ServiceProvider, request, AuthenticationManager, wrapped, cancellationToken);
                HubconContext.UseContext(callContext);

                return _client.CallAsync(request, context, cancellationToken);
            }
            else
            {
                throw new Exception($"Could not find the operation '{methodSignature}'.");
            }
        }

        public override Task<T> IngestAsync<T>(string methodSignature, Dictionary<string, object> arguments, CancellationToken cancellationToken)
        {
            if (_operations.TryGetValue(methodSignature, out IClientOperationContext? context))
            {
                OperationRequest request = new OperationRequest(context.MethodSignature, SimpleContractName, arguments!);
                var wrapped = WrappedContext.Current;

                using var scope = rootServiceProvider.CreateScope();
                var callContext = new CallContext(scope.ServiceProvider, request, AuthenticationManager, wrapped, cancellationToken);
                HubconContext.UseContext(callContext);

                return _client.Ingest<T>(request, context, cancellationToken);
            }
            else
            {
                throw new Exception($"Could not find the operation '{methodSignature}'.");
            }
        }

        public override Task IngestAsync(string methodSignature, Dictionary<string, object> arguments, CancellationToken cancellationToken)
        {
            if (_operations.TryGetValue(methodSignature, out IClientOperationContext? context))
            {
                OperationRequest request = new OperationRequest(context.MethodSignature, SimpleContractName, arguments!);
                var wrapped = WrappedContext.Current;

                using var scope = rootServiceProvider.CreateScope();
                var callContext = new CallContext(scope.ServiceProvider, request, AuthenticationManager, wrapped, cancellationToken);
                HubconContext.UseContext(callContext);

                return _client.Ingest<JsonElement>(request, context, cancellationToken);
            }
            else
            {
                throw new Exception($"Could not find the operation '{methodSignature}'.");
            }

        }

        public override async IAsyncEnumerable<T> StreamAsync<T>(string methodSignature, Dictionary<string, object> arguments, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            if (_operations.TryGetValue(methodSignature, out IClientOperationContext? context))
            {
                OperationRequest request = new OperationRequest(context.MethodSignature, SimpleContractName, arguments!);
                var wrapped = WrappedContext.Current;

                using var scope = rootServiceProvider.CreateScope();
                var callContext = new CallContext(scope.ServiceProvider, request, AuthenticationManager, wrapped, cancellationToken);
                HubconContext.UseContext(callContext);

                IAsyncEnumerable<JsonElement> stream = _client.GetStream(request, context, cancellationToken);

                await foreach(var item in _converter.ConvertStream<T>(stream, cancellationToken))
                {
                    yield return item;
                }
            }
            else
            {
                throw new Exception($"Could not find the operation '{methodSignature}'.");
            }
        }

        // Accessor
        public IAuthenticationManager AuthenticationManager => _clientOptions.AuthenticationManagerFactory.GetValue<IAuthenticationManager>(rootServiceProvider);
    }
}
