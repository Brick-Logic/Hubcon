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
        private static readonly ConcurrentDictionary<string, bool> _attributeCache = new();
        private static ConcurrentDictionary<MethodInfo, (string, string, string)> _routeCache = new ConcurrentDictionary<MethodInfo, (string, string, string)>();


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

        public static (string EndpointGroup, string Endpoint, string FullRoute) GetRoute(this MethodInfo method)
        {
            if (_routeCache.TryGetValue(method, out var route))
            {
                return route;
            }
            else
            {
                var cleanName = NamingHelper.GetCleanName(method.DeclaringType!.Name);
                var result = "/" + method.Name;
                var fullRoute = "/" + cleanName + "/" + method.Name;
                var combined = (cleanName, result, fullRoute);
                _routeCache.TryAdd(method, combined);
                return combined;
            }
        }
    }
}
