using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Shared.Core.Extensions
{
    public static class TypeExtensions
    {
        private static volatile ImmutableDictionary<Type, bool> _cache = ImmutableDictionary<Type, bool>.Empty;

        public static bool IsAsyncEnumerable(this Type type)
        {
            if (_cache.TryGetValue(type, out var cached))
                return cached;

            var computed = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>);

            // Atomic update sin locks
            ImmutableInterlocked.Update(ref _cache, (dict, kvp) => dict.SetItem(kvp.Key, kvp.Value), new KeyValuePair<Type, bool>(type, computed));

            return computed;
        }
    }
}
