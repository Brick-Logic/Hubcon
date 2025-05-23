using Hubcon.Client.Abstractions.Interfaces;

namespace Hubcon.Client.Builder
{
    public abstract class RemoteServerModule : IRemoteServerModule
    {
        public abstract void Configure(IServerModuleConfiguration configuration);
    }
}