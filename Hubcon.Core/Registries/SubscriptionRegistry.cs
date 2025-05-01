using Hubcon.Core.Models.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Core.Registries
{
    public class SubscriptionRegistry : ISubscriptionRegistry
    {
        private Dictionary<string, Dictionary<string, Dictionary<string, ISubscription>>> _contractHandlers = new();

        public void RegisterHandler(string clientId, string contractName, string handlerName, ISubscription handler)
        {
            if (string.IsNullOrEmpty(clientId))
                throw new ArgumentNullException(nameof(clientId), $"El parametro {nameof(clientId)} no puede ser nulo.");
            if (string.IsNullOrEmpty(contractName))
                throw new ArgumentNullException(nameof(contractName), $"El parametro {nameof(contractName)} no puede ser nulo.");
            if (string.IsNullOrEmpty(handlerName))
                throw new ArgumentNullException(nameof(handlerName), $"El parametro {nameof(handlerName)} no puede ser nulo.");

            if (!_contractHandlers.TryGetValue(clientId, out var clientHandlers))
            {
                clientHandlers = new Dictionary<string, Dictionary<string, ISubscription>>();
                _contractHandlers[clientId] = clientHandlers;
            }

            if (!clientHandlers.TryGetValue(contractName, out var contractHandlers))
            {
                contractHandlers = new Dictionary<string, ISubscription>();
                clientHandlers[contractName] = contractHandlers;
            }

            contractHandlers[handlerName] = handler;
        }

        public ISubscription? GetHandler(string clientId, string contractName, string handlerName)
        {
            if (string.IsNullOrEmpty(clientId))
                throw new ArgumentNullException(nameof(clientId), $"El parametro {nameof(clientId)} no puede ser nulo.");
            if (string.IsNullOrEmpty(contractName))
                throw new ArgumentNullException(nameof(contractName), $"El parametro {nameof(contractName)} no puede ser nulo.");
            if (string.IsNullOrEmpty(handlerName))
                throw new ArgumentNullException(nameof(handlerName), $"El parametro {nameof(handlerName)} no puede ser nulo.");

            if (_contractHandlers.TryGetValue(clientId, out var clientHandlers) &&
                clientHandlers.TryGetValue(contractName, out var contractHandlers) &&
                contractHandlers.TryGetValue(handlerName, out ISubscription? handler))
            {
                return handler;
            }

            return default!;
        }

        public bool RemoveHandler(string clientId, string contractName, string handlerName)
        {
            if (string.IsNullOrEmpty(clientId))
                throw new ArgumentNullException(nameof(clientId), $"El parametro {nameof(clientId)} no puede ser nulo.");
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
