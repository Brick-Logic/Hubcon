using Hubcon.Shared.Abstractions.Standard.Interfaces;

namespace Hubcon.Server.Abstractions.Interfaces
{
    /// <summary>
    /// Defines a registry for caching and managing active client proxy instances.
    /// This registry facilitates the retrieval of specific client controllers by their unique identifiers and types.
    /// </summary>
    public interface IClientRegistry
    {
        /// <summary>
        /// Registers a client proxy instance associated with a specific controller type and client identifier.
        /// </summary>
        /// <param name="controllerType">The <see cref="Type"/> of the controller being registered.</param>
        /// <param name="clientId">The unique identifier for the specific client instance.</param>
        /// <param name="client">The <see cref="IControllerContract"/> instance to be cached.</param>
        void RegisterClient(Type controllerType, string clientId, IControllerContract client);

        /// <summary>
        /// Attempts to retrieve a cached client proxy instance of a specific type.
        /// </summary>
        /// <typeparam name="T">The expected type of the client proxy, which must implement <see cref="IControllerContract"/>.</typeparam>
        /// <param name="controllerType">The <see cref="Type"/> of the controller to look up.</param>
        /// <param name="clientId">The unique identifier for the specific client instance.</param>
        /// <returns>The cached client instance if found and castable to <typeparamref name="T"/>; otherwise, <see langword="null"/>.</returns>
        T? TryGetClient<T>(Type controllerType, string clientId) where T : IControllerContract;

        /// <summary>
        /// Removes a client proxy instance from the registry, effectively clearing it from the cache.
        /// </summary>
        /// <param name="controllerType">The <see cref="Type"/> of the controller to unregister.</param>
        /// <param name="clientId">The unique identifier of the client instance to remove.</param>
        void UnregisterClient(Type controllerType, string clientId);
    }
}