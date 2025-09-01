using Hubcon.Shared.Abstractions.Enums;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Abstractions.Standard.Interfaces;
using Hubcon.Shared.Core.Websockets.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Client.Core.Configurations
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class ContractOptions<T> : IContractOptions, IContractConfigurator<T> where T : IControllerContract
    {
        public Type ContractType { get; } = typeof(T);

        public ConcurrentDictionary<string, IOperationOptions> OperationOptions { get; } = new();

        private bool? websocketMethodsEnabled;
        public bool WebsocketMethodsEnabled => websocketMethodsEnabled ?? false;

        ConcurrentDictionary<HookType, Func<HookContext, Task>> _hooks = new();
        public IReadOnlyDictionary<HookType, Func<HookContext, Task>> Hooks => _hooks;

        public bool RemoteCancellationIsAllowed { get; private set; }
        
        public IContractConfigurator<T> UseWebsocketMethods(bool value = true)
        {
            websocketMethodsEnabled ??= value;
            return this;
        }

        public Task CallHook(HookType hookType, HookContext context)
        {
            if (_hooks.TryGetValue(hookType, out var hookDelegate))
            {
                return hookDelegate(context);
            }
            return Task.CompletedTask;
        }

        public Task CallHook(HookType type, IServiceProvider services, IOperationRequest request, CancellationToken cancellationToken, object? result = null, Exception? exception = null)
        {
            if (_hooks.TryGetValue(type, out var hookDelegate))
            {
                return hookDelegate(new HookContext(type, services, request, cancellationToken, result, exception));
            }

            return Task.CompletedTask;
        }

        public IOperationOptions? GetOperationOptions(string operationName)
        {
            if (OperationOptions.TryGetValue(operationName, out IOperationOptions? operationOptions))
            {
                return operationOptions;
            }

            return null;
        }

        public bool IsWebsocketOperation(string operationName)
        {           
            if (OperationOptions.TryGetValue(operationName, out IOperationOptions? operationOptions))
            {
                return operationOptions.TransportType switch
                {
                    TransportType.Default => WebsocketMethodsEnabled,
                    TransportType.Websockets => true,
                    TransportType.Http => false,
                    _ => WebsocketMethodsEnabled
                };
            }

            return false;        
        }

        public IContractConfigurator<T> ConfigureOperations(Action<Shared.Abstractions.Interfaces.IOperationSelector<T>> configure)
        {
            var options = new GlobalOperationConfigurator<T>(OperationOptions);
            configure?.Invoke(options);
            return this;
        }

        public IContractConfigurator<T> AddHook(HookType hookType, Func<HookContext, Task> hookDelegate)
        {
            _hooks.TryAdd(hookType, hookDelegate);
            return this;
        }

        public IContractConfigurator<T> AllowRemoteCancellation(bool value = true)
        {
            RemoteCancellationIsAllowed = value;
            return this;
        }
    }
}
