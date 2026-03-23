using Hubcon.Server.Abstractions.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon
{
    /// <summary>
    /// Defines a contract for a high-performance, object-based caching layer used 
    /// during Hubcon operation execution and metadata resolution.
    /// </summary>
    public interface IOperationCache
    {
        /// <summary>
        /// Attempts to retrieve a cached value of type <typeparamref name="T"/> associated with the specified key.
        /// </summary>
        /// <typeparam name="T">The type of the cached value.</typeparam>
        /// <param name="key">The unique identifier for the cached item.</param>
        /// <param name="value">When this method returns, contains the value associated with the specified key, if found; otherwise, the default value.</param>
        /// <returns><see langword="true"/> if the key was found in the cache; otherwise, <see langword="false"/>.</returns>
        bool TryGetValue<T>(object key, out T? value);

        /// <summary>
        /// Adds or updates a value in the cache with an optional callback for when the item is removed.
        /// </summary>
        /// <typeparam name="T">The type of the value being cached.</typeparam>
        /// <param name="key">The unique identifier for the cached item.</param>
        /// <param name="value">The instance to store in the cache.</param>
        /// <param name="postEvictionCallback">An optional action to execute after the item is removed from the cache.</param>
        /// <returns>The instance that was just cached.</returns>
        T Set<T>(object key, T value, Action? postEvictionCallback = null) where T : class;

        /// <summary>
        /// Explicitly removes the value associated with the specified key from the cache.
        /// </summary>
        /// <param name="key">The unique identifier for the item to remove.</param>
        void Remove(object key);

        /// <summary>
        /// Retrieves an existing cached item or creates a new one using the provided factory if the key is missing.
        /// </summary>
        /// <typeparam name="T">The type of the value.</typeparam>
        /// <param name="key">The unique identifier for the cached item.</param>
        /// <param name="factory">A function that generates the value if it is not present in the cache.</param>
        /// <param name="postEvictionCallback">An optional action to execute after the item is removed from the cache.</param>
        /// <returns>The existing or newly created value.</returns>
        T? GetOrCreate<T>(object key, Func<T?> factory, Action? postEvictionCallback = null) where T : class;
    }
}
