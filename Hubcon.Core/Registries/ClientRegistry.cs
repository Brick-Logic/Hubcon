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
        private static Dictionary<Type, Dictionary<string, ICommunicationContract>> Clients { get; } = new();

        public void RegisterClient(Type controllerType, string clientId, ICommunicationContract client)
        {
            if (client == null)
                throw new ArgumentNullException(nameof(client), "El cliente no puede ser nulo.");

            if (string.IsNullOrWhiteSpace(clientId))
                throw new ArgumentNullException(nameof(client), "El clientId no puede ser nulo.");

            if (!Clients.TryGetValue(controllerType, out Dictionary<string, ICommunicationContract>? clientList))
                Clients[controllerType] = clientList = new();
            
            if (clientList.ContainsKey(clientId))
                throw new ArgumentException($"El cliente con ID {clientId} ya está registrado.");

            clientList[clientId] = client;
        }

        public void UnregisterClient(Type controllerType, string clientId)
        {
            if (string.IsNullOrWhiteSpace(clientId))
                throw new ArgumentNullException(nameof(clientId), "El clientId no puede ser nulo.");

            if (Clients.TryGetValue(controllerType, out Dictionary<string, ICommunicationContract>? clientList))
                clientList.Remove(clientId);
            
        }

        public T? TryGetClient<T>(Type controllerType, string clientId) where T : ICommunicationContract
        {
            if (string.IsNullOrWhiteSpace(clientId))
                throw new ArgumentNullException(nameof(clientId), "El clientId no puede ser nulo.");

            if (Clients.TryGetValue(controllerType, out Dictionary<string, ICommunicationContract>? clientList))
            {
                if (clientList.TryGetValue(clientId, out ICommunicationContract? client))
                    return (T)client;
            }

            return default;
        }
    }
}
