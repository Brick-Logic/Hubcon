namespace Hubcon.Client.Abstractions.Interfaces
{
    /// <summary>
    /// Defines the contract for a Hubcon server module configuration from the client's perspective.
    /// Used to register and configure specific server-side features, endpoints, and behaviors 
    /// within the client-side initialization pipeline.
    /// </summary>
    public interface IRemoteServerModule
    {
        /// <summary>
        /// Configures the server module using the provided configuration builder.
        /// This method is typically called during the client setup phase to define 
        /// module-specific settings such as transports, interceptors, and contract mappings.
        /// </summary>
        /// <param name="server">The <see cref="IServerModuleConfiguration"/> instance used to define the module's behavior.</param>
        void Configure(IServerModuleConfiguration server);
    }
}