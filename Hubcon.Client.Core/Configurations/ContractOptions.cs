using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
#pragma warning disable CS1591
namespace Hubcon.Client.Core.Configurations
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class ContractOptions<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T> : IContractOptions, IContractConfigurator<T> where T : IControllerContract
    {
        public Type ContractType { get; } = typeof(T);

        public ConcurrentDictionary<string, IOperationOptions> OperationOptions { get; } = new();

        ConcurrentDictionary<HookType, Func<IInvocationContext, Task>> _hooks = new();
        public IReadOnlyDictionary<HookType, Func<IInvocationContext, Task>> Hooks => _hooks;

        public bool? RemoteCancellationIsAllowed { get; private set; }

        public HubconTransportAttribute? TransportType { get; private set; }

        public bool? AuthIsEnabled { get; private set; }
        public Dictionary<string, Func<IServiceProvider, string>> HeaderProviders { get; } = new();

        public Task CallHook(HookType hookType, IInvocationContext context)
        {
            return _hooks.GetOrAdd(hookType, _ => Task.CompletedTask).Invoke(context);
        }

        public IOperationOptions GetOperationOptions(string operationName, MemberInfo memberInfo)
        {
            return OperationOptions.GetOrAdd(operationName, name => new OperationOptions(memberInfo));
        }

        public IContractConfigurator<T> ConfigureOperations(Action<IOperationSelector<T>> configure)
        {
            var options = new GlobalOperationConfigurator<T>(OperationOptions);
            configure?.Invoke(options);
            return this;
        }

        public IOperationConfigurator ForOperation<TDelegate>(Expression<Func<T, TDelegate>> expression)
        {
           return new GlobalOperationConfigurator<T>(OperationOptions).Configure(expression);
        }

        public IContractConfigurator<T> AddHook(HookType hookType, Func<IInvocationContext, Task> hookDelegate)
        {
            _hooks.TryAdd(hookType, hookDelegate);
            return this;
        }

        public IContractConfigurator<T> AllowRemoteCancellation(bool value = true)
        {
            RemoteCancellationIsAllowed = value;
            return this;
        }

        public IContractConfigurator<T> SetDefaultTransport<TTransport>() where TTransport : HubconTransportAttribute, new()
        {
            TransportType = HubconTransportAttribute.GetDefault<TTransport>();
            return this;
        }

        public IContractConfigurator<T> UseWebSockets()
        {
            TransportType = HubconTransportAttribute.GetDefault<WebSocketTransport>();
            return this;
        }

        public IContractConfigurator<T> UseHttp()
        {
            TransportType = HubconTransportAttribute.GetDefault<WebSocketTransport>();
            return this;
        }

        public IContractConfigurator<T> UseNonHubconHttp()
        {
            TransportType = HubconTransportAttribute.GetDefault<NonHubconHttpTransport>();
            return this;
        }

        public IContractConfigurator<T> AddHeaderProvider(string key, Func<IServiceProvider, string> valueProvider)
        {
            HeaderProviders.TryAdd(key, valueProvider);
            return this;
        }

        public IContractConfigurator<T> EnableAuth(bool enabled)
        {
            AuthIsEnabled ??= enabled;
            return this;
        }
    }
}