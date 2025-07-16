using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Server.Core.Subscriptions;
using Hubcon.Shared.Abstractions.Interfaces;
using System.Collections.Concurrent;
using System.Reflection;

namespace Hubcon.Server.Core.Routing.Registries
{
    public class LiveSubscriptionRegistry : ILiveSubscriptionRegistry
    {
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ConcurrentDictionary<string, ISubscriptionDescriptor>>> _contractHandlers = new();
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, PropertyInfo>> _descriptorMetadata = new();

        public ISubscriptionDescriptor RegisterHandler(string clientId, string contractName, string subscriptionName, ISubscription handler)
        {
            if (string.IsNullOrWhiteSpace(clientId))
                clientId = "anonymous";

            if (string.IsNullOrWhiteSpace(contractName))
                throw new ArgumentNullException(nameof(contractName));

            if (string.IsNullOrWhiteSpace(subscriptionName))
                throw new ArgumentNullException(nameof(subscriptionName));

            var sourceProperty = GetSubscriptionMetadata(contractName, subscriptionName);
            if (sourceProperty == null)
                throw new ArgumentException($"No se encontró metadata de suscripción para '{contractName}.{subscriptionName}'.", nameof(subscriptionName));

            var descriptor = new SubscriptionDescriptor(contractName, sourceProperty, handler);

            var clientHandlers = _contractHandlers.GetOrAdd(clientId, _ => new ConcurrentDictionary<string, ConcurrentDictionary<string, ISubscriptionDescriptor>>());
            var contractHandlers = clientHandlers.GetOrAdd(contractName, _ => new ConcurrentDictionary<string, ISubscriptionDescriptor>());

            // Intenta agregar, si ya existe la clave no hace nada
            contractHandlers.TryAdd(descriptor.DescriptorSignature, descriptor);

            return descriptor;
        }

        public PropertyInfo? GetSubscriptionMetadata(string contractName, string descriptorSignature)
        {
            if (string.IsNullOrWhiteSpace(contractName) || string.IsNullOrWhiteSpace(descriptorSignature))
                return null;

            if (_descriptorMetadata.TryGetValue(contractName, out var signatures))
            {
                if (signatures.TryGetValue(descriptorSignature, out PropertyInfo? value))
                    return value;
            }

            return null;
        }

        public void RegisterSubscriptionMetadata(string contractName, string descriptorSignature, PropertyInfo info)
        {
            if (string.IsNullOrWhiteSpace(contractName) || string.IsNullOrWhiteSpace(descriptorSignature) || info == null)
                throw new ArgumentException("Los parámetros no pueden ser nulos ni vacíos.");

            var contracts = _descriptorMetadata.GetOrAdd(contractName, _ => new ConcurrentDictionary<string, PropertyInfo>());
            contracts.TryAdd(descriptorSignature, info); // Ignora si ya existe
        }

        public ISubscriptionDescriptor? GetHandler(string clientId, string contractName, string handlerName)
        {
            if (string.IsNullOrWhiteSpace(clientId))
                clientId = "anonymous";

            if (string.IsNullOrWhiteSpace(contractName) || string.IsNullOrWhiteSpace(handlerName))
                return null;

            if (_contractHandlers.TryGetValue(clientId, out var clientHandlers) &&
                clientHandlers.TryGetValue(contractName, out var contractHandlers) &&
                contractHandlers.TryGetValue(handlerName, out ISubscriptionDescriptor? handler))
            {
                return handler;
            }

            return null;
        }

        public bool RemoveHandler(string clientId, string contractName, string handlerName)
        {
            if (string.IsNullOrWhiteSpace(clientId))
                clientId = "anonymous";

            if (string.IsNullOrWhiteSpace(contractName) || string.IsNullOrWhiteSpace(handlerName))
                return false;

            if (_contractHandlers.TryGetValue(clientId, out var clientHandlers) &&
                clientHandlers.TryGetValue(contractName, out var contractHandlers))
            {
                if (contractHandlers.TryRemove(handlerName, out _))
                {
                    if (contractHandlers.IsEmpty)
                        clientHandlers.TryRemove(contractName, out _);

                    if (clientHandlers.IsEmpty)
                        _contractHandlers.TryRemove(clientId, out _);

                    return true;
                }
            }

            return false;
        }
    }

}
