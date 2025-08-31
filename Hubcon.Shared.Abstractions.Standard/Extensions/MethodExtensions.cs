using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace Hubcon.Shared.Abstractions.Standard.Extensions
{
    public static class MethodExtensions
    {
        private static ConcurrentDictionary<MethodInfo, string> _signatureCache = new ConcurrentDictionary<MethodInfo, string>();
        private static ConcurrentDictionary<MethodInfo, string> _hashedSignatureCache = new ConcurrentDictionary<MethodInfo, string>();

        public static string ToHashedMethodString(string methodName, string parameters)
        {
            if (string.IsNullOrEmpty(parameters))
                return methodName;

            // Hash corto (8 caracteres hexadecimales)
            var sha1 = SHA1.Create();

            var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(parameters));
            string hashStr = BitConverter.ToString(hash).Replace("-", "").Substring(0, 6);

            sha1.Dispose();

            return $"{methodName}_{hashStr}";
        }

        public static string GetMethodSignature(this MethodInfo method, bool useHashed = true)
        {
            if (useHashed)
            {
                return _hashedSignatureCache.GetOrAdd(method, x =>
                {
                    string methodName = method.Name;
                    string parameters = string.Join(", ",
                        method.GetParameters()
                        .Select(p => GetRuntimeTypeString(p.ParameterType)));

                    if (method.GetParameters().Length > 0)
                        parameters = $"({parameters})";

                    return ToHashedMethodString(methodName, parameters);
                });
            }
            else
            {
                return _signatureCache.GetOrAdd(method, x =>
                {
                    string methodName = method.Name;
                    string parameters = string.Join(", ",
                        method.GetParameters()
                              .Select(p => GetRuntimeTypeString(p.ParameterType)));

                    if (method.GetParameters().Length > 0)
                        parameters = $"({parameters})";

                    return $"{methodName}{parameters}";
                });
            }
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
