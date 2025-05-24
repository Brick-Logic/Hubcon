using Microsoft.Extensions.DependencyInjection;

namespace Hubcon.Client.Abstractions.Interfaces
{
    public interface IClientBuilderRegistry
    {
        bool GetClientBuilder(Type contractType, out IClientBuilder? value);
        void RegisterModule<TRemoteServerModule>(IServiceCollection services) where TRemoteServerModule : IRemoteServerModule, new();
    }
}
