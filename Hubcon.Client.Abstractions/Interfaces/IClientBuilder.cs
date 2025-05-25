using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Standard.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Hubcon.Client.Abstractions.Interfaces
{
    public interface IClientBuilder
    {
        Type? AuthenticationManagerType { get; set; }
        Uri? BaseUri { get; set; }
        List<Type> Contracts { get; }
        bool UseSecureConnection { get; set; }

        T GetOrCreateClient<T>(IServiceProvider services) where T : IControllerContract;
        object GetOrCreateClient(Type contractType, IServiceProvider services);
        void LoadContractProxy(Type contractType, IServiceCollection services);
        void UseAuthenticationManager<T>(IServiceCollection services) where T : class, IAuthenticationManager;
    }
}