namespace Hubcon.Server.Abstractions.Interfaces
{
    /// <summary>
    /// Defines a thread-safe registry for caching and retrieving granular permissions 
    /// associated with a specific security token or session identifier.
    /// </summary>
    public interface IPermissionRegistry
    {
        /// <summary>
        /// Stores or updates a specific permission state for a token with a defined 
        /// time-to-live (TTL).
        /// </summary>
        /// <param name="tokenId">The unique identifier for the security token or session (e.g., JTI or Session ID).</param>
        /// <param name="permission">The unique string identifier for the permission or claim being stored.</param>
        /// <param name="isAllowed">A value indicating whether the permission is granted (<see langword="true"/>) or denied (<see langword="false"/>).</param>
        /// <param name="ttl">The duration for which this permission state should remain valid in the cache.</param>
        void Set(string tokenId, string permission, bool isAllowed, TimeSpan ttl);

        /// <summary>
        /// Attempts to retrieve the cached state of a specific permission for the given token.
        /// </summary>
        /// <param name="tokenId">The unique identifier for the security token or session.</param>
        /// <param name="permission">The identifier for the permission to check.</param>
        /// <param name="isAllowed">When this method returns, contains the cached permission state if the key was found.</param>
        /// <returns><see langword="true"/> if the permission state was found and is not expired; otherwise, <see langword="false"/>.</returns>
        bool TryGet(string tokenId, string permission, out bool isAllowed);
    }
}
