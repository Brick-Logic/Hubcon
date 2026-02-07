using Hubcon.Shared.Abstractions.Enums;
using Hubcon.Shared.Abstractions.Models;
using System;
using System.Threading.Tasks;

namespace Hubcon.Shared.Abstractions.Interfaces
{
    public interface IContractConfigurator<T>
    {
        public IContractConfigurator<T> SetDefaultTransport<TTransport>() where TTransport : HubconTransportAttribute, new();
        public IContractConfigurator<T> ConfigureOperations(Action<IOperationSelector<T>> selector);
        public IContractConfigurator<T> AddHook(HookType hookType, Func<IInvocationContext, Task> hookDelegate);
        public IContractConfigurator<T> AllowRemoteCancellation(bool value = true);
        public IContractConfigurator<T> UseWebSockets();
        public IContractConfigurator<T> UseHttp();
        public IContractConfigurator<T> UseNonHubconHttp();
        public IContractConfigurator<T> AddHeaderProvider(string key, Func<IServiceProvider, string> valueProvider);
        IContractConfigurator<T> EnableAuth(bool enabled);
    }
}
