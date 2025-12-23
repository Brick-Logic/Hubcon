using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace Hubcon.Shared.Core.Extensions
{
    public static class PropertyInfoExtensions
    {
        private static readonly ConcurrentDictionary<string, bool> _attributeCache = new();

        public static bool HasCustomAttribute<TCustomAttribute>(this PropertyInfo method) where TCustomAttribute : Attribute
        {
            var methodName = $"{method.ReflectedType!.Name}_{method.Name}_{typeof(TCustomAttribute).FullName}";

            var result = _attributeCache.TryGetValue(methodName, out var hasAttribute);

            if (!result)
            {
                hasAttribute = method.IsDefined(typeof(TCustomAttribute), false);
                _attributeCache[methodName] = hasAttribute;
            }

            return hasAttribute;
        }
    }
}
