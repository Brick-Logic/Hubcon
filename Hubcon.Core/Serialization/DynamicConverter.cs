using Hubcon.Core.Abstractions.Interfaces;
using Newtonsoft.Json;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Hubcon.Core.Serialization
{
    public class DynamicConverter : IDynamicConverter
    {
        public Dictionary<Delegate, Type[]> TypeCache { get; private set; } = new();

        private readonly JsonSerializerOptions jsonSerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            MaxDepth = 64,

        };

        public object?[] SerializeArgs(object?[] args)
        {
            if(args == null)
                return Array.Empty<object>();
            if (args.Length == 0) 
                return Array.Empty<object>();

            for (int i = 0; i < args.Length; i++)
                args[i] = JsonConvert.SerializeObject(args[i]);

            return args;
        }

        public object?[] DeserializeArgs(Type[] types, object?[] args)
        {
            if (types.Length == 0) return Array.Empty<object>();

            if (types.Length != args.Length)
                throw new ArgumentException("El número de tipos y valores debe coincidir.");

            for (int i = 0; i < types.Length; i++)
            {
                if (typeof(IAsyncEnumerable<JsonElement>).IsAssignableFrom(types[i]))
                    args[i] = (IAsyncEnumerable<JsonElement>?)args[i];

                if (typeof(IAsyncEnumerable<>).IsAssignableFrom(types[i]))
                    args[i] = (IAsyncEnumerable<object>?)args[i];

                args[i] = JsonConvert.DeserializeObject($"{args[i]}", types[i]);
            }

            return args;
        }

        public object?[] DeserializedArgs(Delegate del, object?[] args)
        {
            if (args.Length == 0) return Array.Empty<object>();

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

        public string? SerializeData(object? data) => data == null ? null : JsonConvert.SerializeObject(data);    
        public object? DeserializeData(Type type, object data) => data == null ? null : JsonConvert.DeserializeObject($"{data}", type);
        public T? DeserializeData<T>(object? data)
        {
            if (data == null) return default;

            if (typeof(IAsyncEnumerable<JsonElement>).IsAssignableFrom(typeof(T)))
                return (T)data;

            if (typeof(IAsyncEnumerable<object>).IsAssignableFrom(typeof(T)))
                return (T)data;

            return (T?)DeserializeData(typeof(T), data);       
        }


        // 1. Convierte un objeto a JsonElement
        public JsonElement SerializeObject(object? value)
        {
            return System.Text.Json.JsonSerializer.SerializeToElement(value, jsonSerializerOptions).Clone();
        }

        // 2. Convierte una colección de objetos a JsonElements
        public IEnumerable<JsonElement> SerializeArgsToJson(IEnumerable<object?> values)
        {
            List<JsonElement> results = new();

            foreach (var val in values)
            {
                results.Add(SerializeObject(val));
            }

            return results;
        }

        // 3. Convierte un JsonElement a un objeto fuertemente tipado
        public object? DeserializeJsonElement(JsonElement element, Type targetType)
        {
            if (element.ValueKind == JsonValueKind.Null)
                return null;

            return element.Clone().Deserialize(targetType, jsonSerializerOptions);
        }

        // 3. Convierte un JsonElement a un objeto fuertemente tipado
        public T? DeserializeJsonElement<T>(JsonElement element)
        {
            if ( element.ValueKind == JsonValueKind.Null || element.ValueKind == JsonValueKind.Undefined)
                return default;

            return element.Clone().Deserialize<T>(jsonSerializerOptions);
        }

        // 4. Convierte una lista de JsonElements a objetos, según tipos dados
        public IEnumerable<object?> DeserializeJsonArgs(IEnumerable<JsonElement> elements, IEnumerable<Type> types)
        {
            List<object?> list = new();
            using var elementEnum = elements.GetEnumerator();
            using var typeEnum = types.GetEnumerator();

            while (elementEnum.MoveNext() && typeEnum.MoveNext())
            {
                list.Add(DeserializeJsonElement(elementEnum.Current, typeEnum.Current));
            }

            return list;
        }

        public async IAsyncEnumerable<T> ConvertStream<T>(IAsyncEnumerable<JsonElement> stream, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (var item in stream.WithCancellation(cancellationToken))
            {
                if (item is T typedItem)
                {
                    yield return typedItem;
                }
                else
                {
                    yield return DeserializeJsonElement<T>(item)!;
                }
            }
        }

        public async IAsyncEnumerable<JsonElement> ConvertToJsonElementStream(IAsyncEnumerable<object?> stream, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var item in stream.WithCancellation(cancellationToken))
            {
                if (item is JsonElement typedItem)
                {
                    yield return typedItem.Clone();
                }
                else
                {
                    var obj = SerializeObject(item)!;
                    yield return obj;
                }
            }
        }
    }
}
