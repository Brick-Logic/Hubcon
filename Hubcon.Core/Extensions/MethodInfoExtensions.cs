using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Core.Extensions
{
    public static class MethodInfoExtensions
    {
        private static readonly Dictionary<string, bool> _attributeCache = new();

        public static bool HasCustomAttribute<TCustomAttribute>(this MethodInfo method) where TCustomAttribute : Attribute
        {
            var methodName = $"{method.ReflectedType!.Name}_{method.Name}";
            if (!_attributeCache.TryGetValue(methodName, out var hasAttribute))
            {
                hasAttribute = method.IsDefined(typeof(TCustomAttribute), false);
                _attributeCache[methodName] = hasAttribute;
            }
            return hasAttribute;
        }
    }
}
