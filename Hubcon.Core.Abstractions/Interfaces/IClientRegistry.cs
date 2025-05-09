using Hubcon.Core.Abstractions.Standard.Interfaces;

namespace Hubcon.Core.Abstractions.Interfaces
{
    public interface IClientRegistry
    {
        void RegisterClient(Type controllerType, string clientId, IControllerContract client);
        T? TryGetClient<T>(Type controllerType, string clientId) where T : IControllerContract;
        void UnregisterClient(Type controllerType, string clientId);
    }
}