using Hubcon.Shared.Abstractions.Interfaces;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Hubcon.Shared.Core.Serialization
{
    public class DynamicConverter(ILogger<DynamicConverter> logger) : IDynamicConverter
    {
        public Dictionary<Delegate, Type[]> TypeCache { get; private set; } = new();

        private readonly JsonSerializerOptions jsonSerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            MaxDepth = 64,
        };

        public IEnumerable<object?> DeserializeArgs(IEnumerable<Type> types, IEnumerable<object?> args)
        {
            if (!types.Any() || !args.Any()) 
                return [];

            if (types.Count() != args.Count())
                return [];

            var typesEnumerator = types.GetEnumerator();
            var argsEnumerator = args.GetEnumerator();
            var list = new List<object?>();

            int i = 0;
            while(typesEnumerator.MoveNext() && argsEnumerator.MoveNext())
            {
                if (argsEnumerator.Current == null)
                    list.Add(null);

                else if (argsEnumerator.Current is JsonElement element)
                    list.Add(JsonSerializer.Deserialize(element, typesEnumerator.Current));

                else if (typeof(IAsyncEnumerable<JsonElement>).IsAssignableFrom(typesEnumerator.Current))
                    list.Add((IAsyncEnumerable<JsonElement>?)argsEnumerator.Current);

                else if (typeof(IAsyncEnumerable<>).IsAssignableFrom(typesEnumerator.Current))
                    list.Add((IAsyncEnumerable<object>?)argsEnumerator.Current);

                else if (argsEnumerator.Current != null)
                    list.Add(JsonSerializer.Deserialize(argsEnumerator.Current.ToString()!, typesEnumerator.Current));

                i++;
            }

            return list;
        }

        private static ConcurrentDictionary<Delegate, Type[]> _delegateParametersCache = new();

        public IEnumerable<object?> DeserializedArgs(Delegate del, IEnumerable<object?> args)
        {
            if (!args.Any()) return [];

            Type[] parameterTypes;

            parameterTypes = _delegateParametersCache.GetOrAdd(del, x => x
                .GetMethodInfo()
                .GetParameters()
                .Where(p => !p.ParameterType.FullName?.Contains("System.Runtime.CompilerServices.Closure") ?? true)
                .Select(p => p.ParameterType)
                .ToArray());           

            return DeserializeArgs(parameterTypes, args);
        }

        public T? DeserializeData<T>(object? data)
        {
            if (data == null) 
                return default;

            else if(data is JsonElement element)
                return JsonSerializer.Deserialize<T>(element, jsonSerializerOptions);

            else if (typeof(T).IsAssignableFrom(data.GetType()))
                return (T)data;

            else
                return default;
        }


        // 1. Convierte un objeto a JsonElement
        public JsonElement SerializeObject(object? value)
        {
            return JsonSerializer.SerializeToElement(value, jsonSerializerOptions);
        }

        public T DeserializeByteArray<T>(byte[] bytes)
        {
            return JsonSerializer.Deserialize<T>(bytes, jsonSerializerOptions)!;
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

            return element.Deserialize(targetType, jsonSerializerOptions);
        }

        // 3. Convierte un JsonElement a un objeto fuertemente tipado
        public T? DeserializeJsonElement<T>(JsonElement element)
        {
            if ( element.ValueKind == JsonValueKind.Null || element.ValueKind == JsonValueKind.Undefined)
                return default;

            return element.Deserialize<T>(jsonSerializerOptions);
        }

        // 4. Convierte una lista de JsonElements a objetos, según tipos dados
        public IEnumerable<object?> DeserializeJsonArgs(IEnumerable<JsonElement> elements, IEnumerable<Type> types)
        {
            List<object?> list = new();

            try
            {
                using var elementEnum = elements.GetEnumerator();
                using var typeEnum = types.GetEnumerator();

                while (elementEnum.MoveNext() && typeEnum.MoveNext())
                {
                    list.Add(DeserializeJsonElement(elementEnum.Current, typeEnum.Current));
                }

                return list;
            }
            catch(Exception ex)
            {
                logger.LogInformation(ex.ToString());
                return [];
            }

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
                    yield return typedItem;
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
