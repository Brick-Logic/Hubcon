namespace Hubcon.Server.Abstractions.Interfaces
{
    public interface IPermissionRegistry
    {
        void Set(string tokenId, string permission, bool isAllowed, TimeSpan ttl);
        bool TryGet(string tokenId, string permission, out bool isAllowed);
    }
}
