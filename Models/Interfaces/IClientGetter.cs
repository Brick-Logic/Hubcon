namespace Hubcon
{
    public interface IClientAccessor
    {
        bool TryGetInstance<T>(Type hubType, string instanceId, out T? instance) 
            where T : IClientController?;
        bool GetAllClients<T>(Type hubType, out IEnumerable<T>? instance)
            where T : IClientController?;
    }
    public interface IClientAccessor<THub, TIClientController>
        where THub : ServerHub
        where TIClientController : IClientController?
    {
        TIClientController GetClient(string instanceId);
        public IEnumerable<string> GetAllClients();
    }

}
