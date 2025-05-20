using Hubcon.Core.Abstractions.Interfaces;

namespace Hubcon.Client
{
    public abstract class RemoteServerModule : IRemoteServerModule
    {
        public abstract void Configure(IServerModuleConfiguration configuration);
    }
}