using Hubcon.Shared.Abstractions.Standard.Interfaces;
using Hubcon.Shared.Abstractions.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Hubcon.Client.Abstractions.Interfaces
{
    public interface IServerModuleConfiguration
    {
        IServerModuleConfiguration Implements<T>() where T : IControllerContract;
        IServerModuleConfiguration UseAuthenticationManager<T>() where T : class, IAuthenticationManager;
        IServerModuleConfiguration WithBaseUrl(string baseUrl);
        IServerModuleConfiguration UseInsecureConnection();
        IServerModuleConfiguration WithPrefix(string prefix);
        IServerModuleConfiguration WithWebsocketEndpoint(string endpoint);
    }
}