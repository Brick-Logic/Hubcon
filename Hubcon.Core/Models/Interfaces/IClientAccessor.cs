namespace Hubcon.Core.Models.Interfaces
{
    public interface IClientAccessor
    {
        TICommunicationContract GetClient<TICommunicationContract>(string instanceId) where TICommunicationContract : ICommunicationContract;
        List<string> GetAllClients();
        void RemoveClient(string instanceId);
    }

#pragma warning disable S2326 // Unused type parameters should be removed
    public interface IClientAccessor<out TICommunicationContract, TIHubconController> : IClientAccessor
#pragma warning restore S2326 // Unused type parameters should be removed
        where TICommunicationContract : ICommunicationContract?
        where TIHubconController : IBaseHubconController
    {
        TICommunicationContract GetOrCreateClient(string instanceId);
    }
}
