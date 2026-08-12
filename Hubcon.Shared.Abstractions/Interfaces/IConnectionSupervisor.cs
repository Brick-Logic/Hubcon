using System;
using System.Threading.Tasks;

namespace Hubcon.Shared.Abstractions.Interfaces
{
    /// <summary>
    /// Defines a centralized supervisor for managing the logical lifecycle and 
    /// expiration of active client connections or sessions.
    /// </summary>
    public interface IConnectionSupervisor
    {
        /// <summary>
        /// Registers a new connection with a specific expiration deadline and a 
        /// callback to execute if the connection is evicted due to expiration.
        /// </summary>
        /// <param name="id">The unique identifier for the connection.</param>
        /// <param name="expiration">The initial <see cref="DateTime"/> when the session should expire.</param>
        /// <param name="heartbeatExpiration">The initial <see cref="DateTime"/> after which the session is no longer considered alive.</param>
        /// <param name="cancellationCallback">The action to perform (e.g., closing streams, clearing cache) when the session expires.</param>
        void Register(string id, long expiration, long heartbeatExpiration, Action cancellationCallback);

        /// <summary>
        /// Asynchronously removes a connection from the supervisor, typically called 
        /// during a clean/graceful logout or disconnect.
        /// </summary>
        /// <param name="id">The identifier of the connection to unregister.</param>
        /// <returns>A <see cref="Task"/> representing the unregistration process.</returns>
        Task UnregisterAsync(string id);

        /// <summary>
        /// Extends or resets the expiration deadline for an active connection, 
        /// usually triggered by "Keep-Alive" pings or active request traffic.
        /// </summary>
        /// <param name="id">The identifier of the connection to update.</param>
        /// <param name="newExpiration">The new <see cref="DateTime"/> for the session timeout.</param>
        void UpdateExpiration(string id, long newExpiration);
        
        /// <summary>
        /// Notifies the connection supervisor that the connection is alive.
        /// </summary>
        /// <param name="id">The identifier of the connection to notify that it's alive.</param>
        void NotifyAlive(string id, long newExpiration);
    }
}
