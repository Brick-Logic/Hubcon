using Hubcon.Client.Abstractions.Interfaces;

namespace Hubcon
{
    /// <summary>
    /// Base class used to implement a remote server module. Remote server modules are used by Hubcon to implement contracts based on their configuration.
    /// </summary>
    public abstract class RemoteServerModule : IRemoteServerModule
    {
        /// <summary>
        /// Main configuration method. Hubcon will call this method to extract all configurations and use them to connect to a server.
        /// </summary>
        /// <param name="server"></param>
        public abstract void Configure(IServerModuleConfiguration server);
    }
}