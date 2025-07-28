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
    }
}
