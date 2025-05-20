using Hubcon.Core.Abstractions.Standard.Interfaces;

namespace Hubcon.Core.Abstractions.Interfaces
{
    public interface IServerModuleConfiguration
    {
        IServerModuleConfiguration AddContract<T>() where T : IControllerContract;
        IServerModuleConfiguration UseAuthenticationManager<T>() where T : IAuthenticationManager;
        IServerModuleConfiguration WithBaseUrl(string baseUrl);
        IServerModuleConfiguration AddMiddleware<T>() where T : IMiddleware;
        IServerModuleConfiguration UseInsecureConnection();
    }
}