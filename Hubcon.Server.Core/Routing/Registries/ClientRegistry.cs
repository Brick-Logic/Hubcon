using Hubcon.Server.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Standard.Interfaces;
using System.Collections.Concurrent;
using System.ComponentModel;

namespace Hubcon.Server.Core.Routing.Registries
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class ClientRegistry : IClientRegistry
    {
        private readonly ConcurrentDictionary<Type, ConcurrentDictionary<string, IControllerContract>> _clients = new();

        public void RegisterClient(Type controllerType, string clientId, IControllerContract client)
        {
            if (controllerType == null)
                throw new ArgumentNullException(nameof(controllerType));
            if (client == null)
                throw new ArgumentNullException(nameof(client), "El cliente no puede ser nulo.");
            if (string.IsNullOrWhiteSpace(clientId))
                throw new ArgumentNullException(nameof(clientId), "El clientId no puede ser nulo o vacío.");

            var clientList = _clients.GetOrAdd(controllerType, _ => new ConcurrentDictionary<string, IControllerContract>());

            if (!clientList.TryAdd(clientId, client))
            {
                throw new ArgumentException($"El cliente con ID '{clientId}' ya está registrado para el controlador '{controllerType.Name}'.");
            }
        }

        public void UnregisterClient(Type controllerType, string clientId)
        {
            if (controllerType == null)
                throw new ArgumentNullException(nameof(controllerType));
            if (string.IsNullOrWhiteSpace(clientId))
                throw new ArgumentNullException(nameof(clientId), "El clientId no puede ser nulo o vacío.");

            if (_clients.TryGetValue(controllerType, out var clientList))
            {
                clientList.TryRemove(clientId, out _);

                // Opcional: limpiar si queda vacío
                if (clientList.IsEmpty)
                {
                    _clients.TryRemove(controllerType, out _);
                }
            }
        }

        public T? TryGetClient<T>(Type controllerType, string clientId) where T : IControllerContract
        {
            if (controllerType == null)
                throw new ArgumentNullException(nameof(controllerType));
            if (string.IsNullOrWhiteSpace(clientId))
                throw new ArgumentNullException(nameof(clientId), "El clientId no puede ser nulo o vacío.");

            if (_clients.TryGetValue(controllerType, out var clientList))
            {
                if (clientList.TryGetValue(clientId, out var client))
                {
                    return client is T typedClient ? typedClient : default;
                }
            }

            return default;
        }
    }

}
