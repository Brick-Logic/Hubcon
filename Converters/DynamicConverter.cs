using Newtonsoft.Json;
using System.Reflection;

namespace Hubcon.Converters
{
    public static class DynamicConverter
    {
        public static Dictionary<Delegate, Type[]> TypeCache { get; private set; } = [];

        public static object?[] SerializeArgs(object?[] args)
        {
            if (args.Length == 0) return [];

            for (int i = 0; i < args.Length; i++)
                args[i] = JsonConvert.SerializeObject(args[i]);

            return args;
        }

        public static object?[] DeserializeArgs(Type[] types, object?[] args)
        {
            if (types.Length == 0) return [];

            if (types.Length != args.Length)
                throw new ArgumentException("El número de tipos y valores debe coincidir.");

            for (int i = 0; i < types.Length; i++)
                args[i] = JsonConvert.DeserializeObject($"{args[i]}", types[i]);

            return args;
        }

        public static object?[] DeserializedArgs(Delegate del, object?[] args)
        {
            if (args.Length == 0) return [];

            Type[] parameterTypes;

            if (TypeCache.TryGetValue(del, out var types))
            {
                parameterTypes = types;
            }
            else
            {
                parameterTypes = del
                .GetMethodInfo()
                .GetParameters()
                .Where(p => !p.ParameterType.FullName?.Contains("System.Runtime.CompilerServices.Closure") ?? true)
                .Select(p => p.ParameterType)
                .ToArray();
            }

            return DeserializeArgs(parameterTypes, args);
        }

        public static string? SerializeData(object data) => data == null ? null : JsonConvert.SerializeObject(data);    
        public static object? DeserializeData(Type type, object data) => data == null ? null : JsonConvert.DeserializeObject($"{data}", type);       
        public static T? DeserializeData<T>(object data) => JsonConvert.DeserializeObject<T>($"{data}");       
    }
}
