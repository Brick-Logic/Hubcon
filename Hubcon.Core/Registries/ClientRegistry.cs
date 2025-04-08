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
        private static Dictionary<string, ICommunicationContract> Clients { get; } = new();

        public void RegisterClient(string clientId, ICommunicationContract client)
        {
            if (Clients.ContainsKey(clientId))
                throw new ArgumentException($"El cliente con ID {clientId} ya está registrado.");

            Clients[clientId] = client;
        }

        public void UnregisterClient(string clientId)
        {
            Clients.Remove(clientId);
        }

        public T? TryGetClient<T>(string clientId) where T : ICommunicationContract
        {
            if (Clients.TryGetValue(clientId, out ICommunicationContract? client))
                return (T)client;

            return default;
        }
    }
}
