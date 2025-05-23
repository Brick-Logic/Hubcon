using Hubcon.Shared.Abstractions.Standard.Interfaces;
using Hubcon.Shared.Abstractions.Interfaces;

namespace Hubcon.Client.Abstractions.Interfaces
{
    public interface IServerModuleConfiguration
    {
        IServerModuleConfiguration AddContract<T>() where T : IControllerContract;
        IServerModuleConfiguration UseAuthenticationManager<T>() where T : IAuthenticationManager;
        IServerModuleConfiguration WithBaseUrl(string baseUrl);
        IServerModuleConfiguration UseInsecureConnection();
    }
}