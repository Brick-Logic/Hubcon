using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace Hubcon.Shared.Abstractions.Standard.Extensions
{
    public static class MethodExtensions
    {
        private readonly static ConcurrentDictionary<(MethodInfo, bool), string> _signatureCache = new ConcurrentDictionary<(MethodInfo, bool), string>();

        public static string ToHashedMethodString(string methodName, string parameters)
        {
            if (string.IsNullOrEmpty(parameters))
                return methodName;

            uint hash = Fnv1a32(parameters.AsSpan());
            string hashStr = ToBase62(hash);

            return $"{methodName}_{hashStr}";
        }

        private const string Base62Chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

        private static string ToBase62(uint value)
        {
            char[] buffer = new char[6]; // máximo necesario para 32 bits en base62
            int pos = buffer.Length;

            do
            {
                buffer[--pos] = Base62Chars[(int)(value % 62)];
                value /= 62;
            }
            while (value > 0);

            return new string(buffer, pos, buffer.Length - pos);
        }

        private static uint Fnv1a32(ReadOnlySpan<char> input)
        {
            const uint fnvPrime = 16777619;
            uint hash = 2166136261;

            foreach (char c in input)
            {
                hash ^= c;
                hash *= fnvPrime;
            }

            return hash;
        }

        public static string GetMethodSignature(this MethodInfo method, bool useHashed = true)
        {
            return _signatureCache.GetOrAdd((method, useHashed), m =>
            {
                var parametersInfo = method.GetParameters();

                string parameters = string.Empty;

                if (parametersInfo.Length > 0)
                {
                    var builder = new StringBuilder();

                    for (int i = 0; i < parametersInfo.Length; i++)
                    {
                        if (i > 0)
                            builder.Append(", ");

                        var param = parametersInfo[i];

                        if (param.ParameterType.IsByRef)
                        {
                            if (param.IsOut)
                                builder.Append("out ");
                            else
                                builder.Append("ref ");

                            builder.Append(GetRuntimeTypeString(param.ParameterType.GetElementType()));
                        }
                        else
                        {
                            builder.Append(GetRuntimeTypeString(param.ParameterType));
                        }
                    }

                    parameters = $"({builder})";
                }

                return useHashed
                    ? ToHashedMethodString(method.Name, parameters)
                    : $"{method.Name}{parameters}";
            });
        }

        private static string GetRuntimeTypeString(Type type)
        {
            if (type.IsByRef)
                return GetRuntimeTypeString(type.GetElementType());

            if (type.IsArray)
            {
                var rank = type.GetArrayRank();
                var commas = new string(',', rank - 1);
                return $"{GetRuntimeTypeString(type.GetElementType())}[{commas}]";
            }

            if (type.IsGenericType)
            {
                var genericDef = type.GetGenericTypeDefinition();
                var genericArgs = type.GetGenericArguments();

                var name = genericDef.FullName;
                name = name.Substring(0, name.IndexOf('`'));
                name = name.Replace('+', '.'); // Nested types fix

                return $"{name}<{string.Join(", ", genericArgs.Select(GetRuntimeTypeString))}>";
            }

            return type.FullName.Replace('+', '.');
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
