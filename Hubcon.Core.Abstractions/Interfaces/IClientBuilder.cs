using Autofac;
using Hubcon.Core.Abstractions.Standard.Interfaces;

namespace Hubcon.Core.Abstractions.Interfaces
{
    public interface IClientBuilder
    {
        Type? AuthenticationManagerType { get; set; }
        Uri? BaseUri { get; set; }
        List<Type> Contracts { get; }
        bool UseSecureConnection { get; set; }

        T GetOrCreateClient<T>(ILifetimeScope lifetimeScope) where T : IControllerContract;
        object GetOrCreateClient(Type contractType, ILifetimeScope lifetimeScope);
        void LoadContractProxy(Type contractType);
    }
}