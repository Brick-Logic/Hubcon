using Hubcon.Shared.Abstractions.Enums;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Standard.Interfaces;
using Hubcon.Shared.Core.Websockets.Interfaces;
using System;
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

        public Dictionary<string, IOperationOptions> OperationOptions { get; } = new();

        private bool? websocketMethodsEnabled;
        public bool WebsocketMethodsEnabled => websocketMethodsEnabled ?? false;

        public IContractConfigurator<T> UseWebsocketMethods(bool value = true)
        {
            websocketMethodsEnabled ??= value;
            return this;
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

        public IContractConfigurator<T> ConfigureOperations(Action<IGlobalOperationConfigurator<T>> configure)
        {
            var options = new GlobalOperationConfigurator<T>(OperationOptions);
            configure?.Invoke(options);
            return this;
        }
    }
}
