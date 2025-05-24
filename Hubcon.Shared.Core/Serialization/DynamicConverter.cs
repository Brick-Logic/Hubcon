using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Hubcon.Shared.Core.Serialization
{
    [JsonSerializable(typeof(bool))]
    [JsonSerializable(typeof(byte))]
    [JsonSerializable(typeof(sbyte))]
    [JsonSerializable(typeof(char))]
    [JsonSerializable(typeof(decimal))]
    [JsonSerializable(typeof(double))]
    [JsonSerializable(typeof(float))]
    [JsonSerializable(typeof(int))]
    [JsonSerializable(typeof(uint))]
    [JsonSerializable(typeof(nint))]
    [JsonSerializable(typeof(nuint))]
    [JsonSerializable(typeof(long))]
    [JsonSerializable(typeof(ulong))]
    [JsonSerializable(typeof(short))]
    [JsonSerializable(typeof(ushort))]
    [JsonSerializable(typeof(string))]
    [JsonSerializable(typeof(object))]
    [JsonSerializable(typeof(JsonElement))]
    [JsonSerializable(typeof(OperationRequest))]
    [JsonSerializable(typeof(IOperationRequest))]
    [JsonSerializable(typeof(SubscriptionRequest))]
    [JsonSerializable(typeof(ISubscriptionRequest))]
    [JsonSerializable(typeof(HubconGraphQLRequest))]
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, GenerationMode = JsonSourceGenerationMode.Metadata)]
    public partial class SerializationContext : JsonSerializerContext
    {
        
    }

    public class DynamicConverter : IDynamicConverter
    {
        private static readonly Dictionary<Type, JsonTypeInfo> _typeInfoMap = new()
        {
            { typeof(bool), SerializationContext.Default.Boolean },
            { typeof(byte), SerializationContext.Default.Byte },
            { typeof(sbyte), SerializationContext.Default.SByte },
            { typeof(char), SerializationContext.Default.Char },
            { typeof(decimal), SerializationContext.Default.Decimal },
            { typeof(double), SerializationContext.Default.Double },
            { typeof(float), SerializationContext.Default.Single },
            { typeof(int), SerializationContext.Default.Int32 },
            { typeof(uint), SerializationContext.Default.UInt32 },
            { typeof(nint), SerializationContext.Default.IntPtr },
            { typeof(nuint), SerializationContext.Default.UIntPtr },
            { typeof(long), SerializationContext.Default.Int64 },
            { typeof(ulong), SerializationContext.Default.UInt64 },
            { typeof(short), SerializationContext.Default.Int16 },
            { typeof(ushort), SerializationContext.Default.UInt16 },
            { typeof(string), SerializationContext.Default.String },
            { typeof(object), SerializationContext.Default.Object },
            { typeof(JsonElement), SerializationContext.Default.Object },
            { typeof(OperationRequest), SerializationContext.Default.OperationRequest },
            { typeof(IOperationRequest), SerializationContext.Default.IOperationRequest },
            { typeof(SubscriptionRequest), SerializationContext.Default.SubscriptionRequest },
            { typeof(ISubscriptionRequest), SerializationContext.Default.ISubscriptionRequest },
            { typeof(HubconGraphQLRequest), SerializationContext.Default.HubconGraphQLRequest },
        };

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
            if (args == null)
                return Array.Empty<object>();
            if (args.Length == 0)
                return Array.Empty<object>();

            for (int i = 0; i < args.Length; i++)
                args[i] = Newtonsoft.Json.JsonConvert.SerializeObject(args[i]);

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

                args[i] = Newtonsoft.Json.JsonConvert.DeserializeObject($"{args[i]}", types[i]);
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

        public string? SerializeData(object? data) => data == null ? null : Newtonsoft.Json.JsonConvert.SerializeObject(data);
        public object? DeserializeData(Type type, object data) => data == null ? null : Newtonsoft.Json.JsonConvert.DeserializeObject($"{data}", type);
        public T? DeserializeData<T>(object? data)
        {
            if (data == null) return default;

            if (typeof(IAsyncEnumerable<JsonElement>).IsAssignableFrom(typeof(T)))
                return (T)data;

            if (typeof(IAsyncEnumerable<object>).IsAssignableFrom(typeof(T)))
                return (T)data;

            return (T?)DeserializeData(typeof(T), data);
        }


        public byte[] SerializeToByteArray(object? value)
        {
            if (value == null)
                return Array.Empty<byte>();

            var type = value.GetType();

            if (_typeInfoMap.TryGetValue(type, out var typeInfo))
            {
                return JsonSerializer.SerializeToUtf8Bytes(value, typeInfo);
            }

            throw new InvalidOperationException($"No JsonTypeInfo registered for type: {type.FullName}");
        }
        

        public JsonElement SerializeObject(object? value)
        {
            if (value == null)
                return JsonDocument.Parse("null").RootElement.Clone();

            var type = value.GetType();


            var bytes = SerializeToByteArray(value);

            if(bytes.Length == 0)
                return JsonDocument.Parse("null").RootElement.Clone();

            using var doc = JsonDocument.Parse(bytes);
            return doc.RootElement.Clone();      
        }

        public T DeserializeByteArray<T>(byte[] bytes)
        {
            var type = typeof(T);
            var typeInfo = _typeInfoMap[type]; // tipo concreto ya registrado
            return (T)JsonSerializer.Deserialize(bytes, typeInfo)!;
        }

        public T DeserializeObject<T>(JsonElement json)
        {
            var type = typeof(T);
            var typeInfo = _typeInfoMap[type]; // tipo concreto ya registrado
            return (T)JsonSerializer.Deserialize(json, typeInfo)!;
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
            if (element.ValueKind == JsonValueKind.Null || element.ValueKind == JsonValueKind.Undefined)
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

    //[JsonSerializable(typeof(bool))]
    //[JsonSerializable(typeof(byte))]
    //[JsonSerializable(typeof(sbyte))]
    //[JsonSerializable(typeof(char))]
    //[JsonSerializable(typeof(decimal))]
    //[JsonSerializable(typeof(double))]
    //[JsonSerializable(typeof(float))]
    //[JsonSerializable(typeof(int))]
    //[JsonSerializable(typeof(uint))]
    //[JsonSerializable(typeof(nint))]
    //[JsonSerializable(typeof(nuint))]
    //[JsonSerializable(typeof(long))]
    //[JsonSerializable(typeof(ulong))]
    //[JsonSerializable(typeof(short))]
    //[JsonSerializable(typeof(ushort))]
    //[JsonSerializable(typeof(string))]
    //[JsonSerializable(typeof(object))]
    //[JsonSerializable(typeof(JsonElement))]
    //[JsonSerializable(typeof(OperationRequest))]
    //[JsonSerializable(typeof(IOperationRequest))]
    //[JsonSerializable(typeof(SubscriptionRequest))]
    //[JsonSerializable(typeof(ISubscriptionRequest))]
    //[JsonSerializable(typeof(HubconGraphQLRequest))]
    //[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, GenerationMode = JsonSourceGenerationMode.Metadata)]
    //internal partial class PrimitiveJsonContext : JsonSerializerContext
    //{
    //}
}
