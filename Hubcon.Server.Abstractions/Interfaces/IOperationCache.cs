using Hubcon.Server.Abstractions.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon
{
    public interface IOperationCache
    {
        bool TryGetValue<T>(object key, out T? value);
        T Set<T>(object key, T value, Action? postEvictionCallback = null) where T : class;
        void Remove(object key);
        T? GetOrCreate<T>(object key, Func<T?> factory, Action? postEvictionCallback = null) where T : class;
    }
}
