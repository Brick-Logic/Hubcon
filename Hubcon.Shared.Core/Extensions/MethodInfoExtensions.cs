using Hubcon.Shared.Abstractions.Standard.Extensions;
using Hubcon.Shared.Core.Tools;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;

namespace Hubcon.Shared.Core.Extensions
{
    public static class MethodInfoExtensions
    {
        private static readonly ConcurrentDictionary<string, bool> _attributeCache = new ConcurrentDictionary<string, bool>();
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

        public static (string EndpointGroup, string Endpoint, string FullRoute) GetRoute(this MethodInfo method, bool useHashed)
        {
            if (_routeCache.TryGetValue(method, out var route))
            {
                return route;
            }
            else
            {
                var cleanName = NamingHelper.GetCleanName(method.DeclaringType!.Name);
                var resultName = useHashed ? method.GetMethodSignature(true) : method.Name;
                var fullRoute = "/" + cleanName + "/" + resultName;
                var combined = (cleanName, resultName, fullRoute);
                _routeCache.TryAdd(method, combined);
                return combined;
            }
        }

        // Tipos “permitidos” considerados primitivos para tu caso
        private static readonly Type[] AllowedTypes = new Type[]
        {
            typeof(byte), typeof(sbyte), typeof(short), typeof(ushort),
            typeof(int), typeof(uint), typeof(long), typeof(ulong),
            typeof(float), typeof(double), typeof(decimal),
            typeof(bool), typeof(char), typeof(string),
            typeof(DateTime), typeof(Guid)
        };

        public static bool AreParametersValid(this MethodInfo method)
        {
            foreach (var param in method.GetParameters())
            {
                if (!IsTypeAllowed(param.ParameterType))
                    return false;
            }
            return true;
        }

        public static bool AreParametersValid(this ParameterInfo[] parameters)
        {
            foreach (var param in parameters)
            {
                if (!IsTypeAllowed(param.ParameterType))
                    return false;
            }
            return true;
        }

        public static bool IsTypeAllowed(this Type type)
        {
            // Nullable<T> → tomar el tipo subyacente
            if (Nullable.GetUnderlyingType(type) != null)
            {
                type = Nullable.GetUnderlyingType(type)!;
            }

            if (AllowedTypes.Contains(type))
                return true;

            if (type.IsEnum)
                return true;

            if (type.IsClass || (type.IsValueType && !type.IsPrimitive))
            {
                var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                if (properties.Length == 0)
                    return false; // clase sin propiedades es inválida

                var result = properties.All(p => TypeIsPrimitive(p.PropertyType));
                return result;
            }

            return false;
        }

        public static bool TypeIsPrimitive(this Type type)
        {
            // Nullable<T> → tomar el tipo subyacente
            if (Nullable.GetUnderlyingType(type) != null)
            {
                type = Nullable.GetUnderlyingType(type)!;
            }

            if (AllowedTypes.Contains(type))
                return true;

            if (type.IsEnum)
                return true;
         
            return false;
        }
    }
}
