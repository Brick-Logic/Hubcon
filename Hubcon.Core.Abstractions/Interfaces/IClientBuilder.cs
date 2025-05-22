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

        T GetOrCreateClient<T>(IComponentContext context) where T : IControllerContract;
        object GetOrCreateClient(Type contractType, IComponentContext context);
        void LoadContractProxy(Type contractType);
        void UseAuthenticationManager<T>() where T : IAuthenticationManager;
    }
}