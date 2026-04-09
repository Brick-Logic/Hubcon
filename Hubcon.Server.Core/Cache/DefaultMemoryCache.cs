using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
#pragma warning disable CS1591


namespace Hubcon.Server.Core.Cache
{
    public class DefaultMemoryCache(IMemoryCache cache) : IOperationCache
    {
        public bool TryGetValue<T>(object key, out T? value) => cache.TryGetValue(key, out value);

        public T Set<T>(object key, T value, Action? postEvictionCallback = null, int expirationMinutes = 15) where T : class
        {
            return cache.Set(key, value, new MemoryCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromMinutes(expirationMinutes))
                .RegisterPostEvictionCallback((_, value, _, _) =>
                {
                    if (value is IDisposable disposable) disposable.Dispose();
                    postEvictionCallback?.Invoke();
                }));
        }

        public void Remove(object key) => cache.Remove(key);

        public T? GetOrCreate<T>(object key, Func<T?> factory, Action? postEvictionCallback = null, int expirationMinutes = 15) where T : class
        {
            return cache.GetOrCreate(key, entry =>
            {
                var opEntry = factory.Invoke();
                entry.Value = opEntry;

                entry
                .SetSlidingExpiration(TimeSpan.FromMinutes(expirationMinutes))
                .RegisterPostEvictionCallback((_, value, _, _) =>
                {
                    if (value is IDisposable disposable) disposable.Dispose();
                    postEvictionCallback?.Invoke();
                });

                return opEntry;
            });
        }
    }
}
