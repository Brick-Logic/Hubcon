namespace Hubcon
{
    public interface IClientManager : IClientAccessor
    {
        TIClientController CreateInstance<THub, TIClientController>(string instanceId)
            where THub : ServerHub
            where TIClientController : IClientController?;
        TIClientController CreateInstance<TIClientController>(Type hubType, string instanceId)
            where TIClientController : IClientController?;

        void RemoveInstance(Type hubType, string instanceId);
    }

    public interface IClientManager<THub, TIClientController> : IClientAccessor<THub, TIClientController>
        where THub : ServerHub
        where TIClientController : IClientController?
    {
        void RemoveInstance(string instanceId);
    }
}
