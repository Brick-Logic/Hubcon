using Autofac;

namespace Hubcon.Client.Abstractions.Interfaces
{
    public interface IClientBuilderRegistry
    {
        bool GetClientBuilder(Type contractType, out IClientBuilder? value);
        void RegisterModule<TRemoteServerModule>(List<Action<ContainerBuilder>> ServicesToInject) where TRemoteServerModule : IRemoteServerModule, new();
    }
}
