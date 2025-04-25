using Hubcon.Core.Models.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Core.Registries
{
    public class ClientRegistry
    {
        private Dictionary<Type, Dictionary<string, IHubconControllerContract>> Clients { get; } = new();

        public void RegisterClient(Type controllerType, string clientId, IHubconControllerContract client)
        {
            if (client == null)
                throw new ArgumentNullException(nameof(client), "El cliente no puede ser nulo.");

            if (string.IsNullOrWhiteSpace(clientId))
                throw new ArgumentNullException(nameof(client), "El clientId no puede ser nulo.");

            if (!Clients.TryGetValue(controllerType, out Dictionary<string, IHubconControllerContract>? clientList))
                Clients[controllerType] = clientList = new();
            
            if (clientList.ContainsKey(clientId))
                throw new ArgumentException($"El cliente con ID {clientId} ya está registrado.");

            clientList[clientId] = client;
        }

        public void UnregisterClient(Type controllerType, string clientId)
        {
            if (string.IsNullOrWhiteSpace(clientId))
                throw new ArgumentNullException(nameof(clientId), "El clientId no puede ser nulo.");

            if (Clients.TryGetValue(controllerType, out Dictionary<string, IHubconControllerContract>? clientList))
                clientList.Remove(clientId);
            
        }

        public T? TryGetClient<T>(Type controllerType, string clientId) where T : IHubconControllerContract
        {
            if (string.IsNullOrWhiteSpace(clientId))
                throw new ArgumentNullException(nameof(clientId), "El clientId no puede ser nulo.");

            if (Clients.TryGetValue(controllerType, out Dictionary<string, IHubconControllerContract>? clientList))
            {
                if (clientList.TryGetValue(clientId, out IHubconControllerContract? client))
                    return (T)client;
            }

            return default;
        }
    }
}
