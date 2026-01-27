using Hubcon.Client.Abstractions.Interfaces;

namespace Hubcon
{
    public abstract class RemoteServerModule : IRemoteServerModule
    {
        public abstract void Configure(IServerModuleConfiguration configuration);
    }
}