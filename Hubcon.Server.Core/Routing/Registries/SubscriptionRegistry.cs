using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Core.Subscriptions;
using System.Reflection;

namespace Hubcon.Server.Core.Routing.Registries
{
    public class LiveSubscriptionRegistry : ILiveSubscriptionRegistry
    {
        private Dictionary<string, Dictionary<string, Dictionary<string, ISubscriptionDescriptor>>> _contractHandlers = new();
        private Dictionary<string, Dictionary<string, PropertyInfo>> _descriptorMetadata = new();

        public ISubscriptionDescriptor RegisterHandler(string clientId, string contractName, string subscriptionName, ISubscription handler)
        {

            if (string.IsNullOrEmpty(clientId))
                clientId = "anonymous";
            if (string.IsNullOrEmpty(contractName))
                throw new ArgumentNullException(nameof(contractName), $"El parametro {nameof(contractName)} no puede ser nulo.");
            if (string.IsNullOrEmpty(subscriptionName))
                throw new ArgumentNullException(nameof(subscriptionName), $"El parametro {nameof(subscriptionName)} no puede ser nulo.");

            var sourceProperty = GetSubscriptionMetadata(contractName, subscriptionName);

            if (sourceProperty == null)
                throw new ArgumentNullException();

            var descriptor = new SubscriptionDescriptor(contractName, sourceProperty, handler);

            if (!_contractHandlers.TryGetValue(clientId, out var clientHandlers))
            {
                clientHandlers = new Dictionary<string, Dictionary<string, ISubscriptionDescriptor>>();
                _contractHandlers[clientId] = clientHandlers;
            }

            if (!clientHandlers.TryGetValue(descriptor.ContractName, out var contractHandlers))
            {
                contractHandlers = new Dictionary<string, ISubscriptionDescriptor>();
                clientHandlers[descriptor.ContractName] = contractHandlers;
            }

            contractHandlers[descriptor.DescriptorSignature] = descriptor;

            return descriptor;
        }

        public PropertyInfo? GetSubscriptionMetadata(string contractName, string descriptorSignature)
        {
            if (_descriptorMetadata.TryGetValue(contractName, out var signatures))
            {
                if(signatures.TryGetValue(descriptorSignature, out PropertyInfo? value))
                    return value;
            }

            return null;
        }

        public void RegisterSubscriptionMetadata(string contractName, string descriptorSignature, PropertyInfo info)
        {
            if (!_descriptorMetadata.TryGetValue(contractName, out var contracts))
            {
                _descriptorMetadata[contractName] = contracts = new();
            }

            contracts.TryAdd(descriptorSignature, info);
        }

        public ISubscriptionDescriptor? GetHandler(string clientId, string contractName, string handlerName)
        {
            if (string.IsNullOrEmpty(clientId))
                clientId = "anonymous";
            if (string.IsNullOrEmpty(contractName))
                throw new ArgumentNullException(nameof(contractName), $"El parametro {nameof(contractName)} no puede ser nulo.");
            if (string.IsNullOrEmpty(handlerName))
                throw new ArgumentNullException(nameof(handlerName), $"El parametro {nameof(handlerName)} no puede ser nulo.");

            if (_contractHandlers.TryGetValue(clientId, out var clientHandlers) &&
                clientHandlers.TryGetValue(contractName, out var contractHandlers) &&
                contractHandlers.TryGetValue(handlerName, out ISubscriptionDescriptor? handler))
            {
                return handler;
            }

            return default!;
        }

        public bool RemoveHandler(string clientId, string contractName, string handlerName)
        {
            if (string.IsNullOrEmpty(clientId))
                clientId = "anonymous";
            if (string.IsNullOrEmpty(contractName))
                throw new ArgumentNullException(nameof(contractName), $"El parametro {nameof(contractName)} no puede ser nulo.");
            if (string.IsNullOrEmpty(handlerName))
                throw new ArgumentNullException(nameof(handlerName), $"El parametro {nameof(handlerName)} no puede ser nulo.");

            if (_contractHandlers.TryGetValue(clientId, out var clientHandlers) &&
                clientHandlers.TryGetValue(contractName, out var contractHandlers) &&
                contractHandlers.Remove(handlerName))
            {
                // Limpieza si el diccionario queda vacío
                if (contractHandlers.Count == 0)
                {
                    clientHandlers.Remove(contractName);
                }

                if (clientHandlers.Count == 0)
                {
                    _contractHandlers.Remove(clientId);
                }

                return true;
            }

            return false;
        }
    }
}
