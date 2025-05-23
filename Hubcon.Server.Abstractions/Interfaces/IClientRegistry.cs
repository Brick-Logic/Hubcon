using Hubcon.Shared.Abstractions.Standard.Interfaces;

namespace Hubcon.Server.Abstractions.Interfaces
{
    public interface IClientRegistry
    {
        void RegisterClient(Type controllerType, string clientId, IControllerContract client);
        T? TryGetClient<T>(Type controllerType, string clientId) where T : IControllerContract;
        void UnregisterClient(Type controllerType, string clientId);
    }
}