using Hubcon.Shared.Abstractions.Enums;
using Hubcon.Shared.Abstractions.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Shared.Abstractions.Interfaces
{
    public interface IContractConfigurator<T>
    {
        public IContractConfigurator<T> UseWebsocketMethods(bool value = true);
        public IContractConfigurator<T> ConfigureOperations(Action<IGlobalOperationConfigurator<T>> configure);
        public IContractConfigurator<T> AddHook(HookType hookType, Func<HookContext, Task> hookDelegate);
        public IContractConfigurator<T> AllowRemoteCancellation(bool value = true);
    }
}
