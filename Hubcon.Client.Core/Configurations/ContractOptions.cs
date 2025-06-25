using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Standard.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Client.Core.Configurations
{
    public class ContractOptions<T> : IContractOptions, IContractConfigurator where T : IControllerContract
    {
        public Type ContractType { get; } = typeof(T);

        private bool? websocketMethodsEnabled;
        public bool WebsocketMethodsEnabled => websocketMethodsEnabled ?? false;

        public IContractConfigurator UseWebsocketMethods(bool value = true)
        {
            websocketMethodsEnabled ??= value;
            return this;
        }
    }
}
