using System;
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
            return _signatureCache.GetOrAdd(method, x =>
            {
                string methodName = method.Name;
                string parameters = string.Join(", ",
                    method.GetParameters()
                          .Select(p => GetRuntimeTypeString(p.ParameterType)));

                return $"{methodName}({parameters})";
            });
        }

        static string GetRuntimeTypeString(Type type)
        {
            if (type.IsGenericType)
            {
                var genericDef = type.GetGenericTypeDefinition(); // ej: IAsyncEnumerable`1
                var typeName = genericDef.FullName ?? genericDef.Name;

                var args = type.GetGenericArguments()
                               .Select(GetRuntimeTypeString);
                return $"{typeName}[{string.Join(",", args)}]";
            }
            else if (type.IsArray)
            {
                return $"{GetRuntimeTypeString(type.GetElementType())}[]";
            }
            else
            {
                return type.FullName ?? type.Name;
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
