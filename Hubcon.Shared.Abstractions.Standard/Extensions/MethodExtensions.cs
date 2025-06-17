using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Hubcon.Shared.Abstractions.Standard.Extensions
{
    public static class MethodExtensions
    {
        private static ConcurrentDictionary<MethodInfo, string> _signatureCache = new ConcurrentDictionary<MethodInfo, string>();
        private static ConcurrentDictionary<MethodInfo, string> _routeCache = new ConcurrentDictionary<MethodInfo, string>();

        public static string GetMethodSignature(this MethodInfo method)
        {
            if(_signatureCache.TryGetValue(method, out var signature))
            {
                return signature;
            }
            else
            {
                List<string> identifiers = new List<string>()
                {
                    method.Name
                };

                identifiers.AddRange(method.GetParameters().Select(p => p.ParameterType.Name));
                var result = string.Join("_", identifiers);
                _signatureCache.TryAdd(method, result);
                return result;
            }
        }

        public static string GetRoute(this MethodInfo method)
        {
            if( _routeCache.TryGetValue(method, out var route))
                return route;
            else
            {
                var result = "/" + method.DeclaringType.Name + "/" + method.Name;
                _routeCache.TryAdd(method, result);
                return result;
            }
        }

        public static string GetContractName(this MethodInfo method)
        {
            return method.DeclaringType.Name;
        }

        public static string GetOperationName(this MethodInfo method)
        {
            return method.Name;
        }
    }
}
