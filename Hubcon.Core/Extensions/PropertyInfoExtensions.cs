using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Core.Extensions
{
    public static class PropertyInfoExtensions
    {
        private static readonly Dictionary<string, bool> _attributeCache = new();

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
