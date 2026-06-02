using System;

namespace Hubcon.Client.Abstractions.Interfaces
{
    /// <summary>
    /// Provides an interface for managing client heartbeat or connectivity monitoring.
    /// </summary>
    public interface IPingManager : IDisposable
    {
        /// <summary>
        /// Starts the active ping process for the connected client endpoint.
        /// Calling this method initiates the regular checks to verify connectivity.
        /// </summary>
        void Start();
    }
}