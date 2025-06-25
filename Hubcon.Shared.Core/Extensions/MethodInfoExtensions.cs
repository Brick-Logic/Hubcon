using Hubcon.Shared.Core.Tools;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Shared.Core.Extensions
{
    public static class MethodInfoExtensions
    {
        private static readonly Dictionary<string, bool> _attributeCache = new();
        private static ConcurrentDictionary<MethodInfo, string> _routeCache = new ConcurrentDictionary<MethodInfo, string>();


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

        public static string GetRoute(this MethodInfo method)
        {
            if (_routeCache.TryGetValue(method, out var route))
            {
                return route;
            }
            else
            {
                var result = "/" + NamingHelper.GetCleanName(method.DeclaringType!.Name) + "/" + method.Name;
                _routeCache.TryAdd(method, result);
                return result;
            }
        }
    }
}
