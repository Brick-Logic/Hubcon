using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Hubcon.Shared.Core.Serialization
{
    [JsonSerializable(typeof(JsonElement))]
    [JsonSerializable(typeof(IReadOnlyDictionary<string, object>))]
    [JsonSerializable(typeof(Dictionary<string, object>))]
    [JsonSerializable(typeof(JsonObject))]
    [JsonSerializable(typeof(JsonArray))]
    [JsonSerializable(typeof(BaseOperationResponse<string>))]
    public partial class SystemTypesContext : JsonSerializerContext
    {
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class DynamicConverter : IDynamicConverter
    {

        public ConcurrentDictionary<Delegate, Type[]> TypeCache { get; private set; } = new();

        public static JsonSerializerOptions JsonSerializerOptions { get; } = HubconJsonDefaults.Options;

        public IEnumerable<object?> DeserializeArgs(IEnumerable<Type> types, IEnumerable<object?> args)
        {
            if (!types.Any() || !args.Any())
                return Enumerable.Empty<object?>();

            if (types.Count() != args.Count())
                return Enumerable.Empty<object?>();

            var typesEnumerator = types.GetEnumerator();
            var argsEnumerator = args.GetEnumerator();
            var list = new List<object?>();

            int i = 0;
            while (typesEnumerator.MoveNext() && argsEnumerator.MoveNext())
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
        private readonly ILogger<DynamicConverter> logger;

        public DynamicConverter(ILogger<DynamicConverter> logger)
        {
            this.logger = logger;
        }

        public IEnumerable<object?> DeserializedArgs(Delegate del, IEnumerable<object?> args)
        {
            if (!args.Any()) return Enumerable.Empty<object?>();

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

            if (data is JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.Null || element.ValueKind == JsonValueKind.Undefined)
                    return default;

                return element.Deserialize<T>(JsonSerializerOptions);
            }

            if (data is string text)
            {
                if (string.IsNullOrEmpty(text))
                    return default;

                return JsonSerializer.Deserialize<T>(text, JsonSerializerOptions);
            }

            // Si ya es del tipo esperado, lo casteamos directamente
            if (data is T t)
                return t;

            // No sabemos cómo deserializar, devolvemos default
            return default;
        }

        public T? DeserializeFromString<T>(string? json)
        {
            if (string.IsNullOrEmpty(json))
                return default;

            return JsonSerializer.Deserialize<T>(json, JsonSerializerOptions);
        }

        // 1. Convierte un objeto a JsonElement
        public JsonElement SerializeObject(object? value)
        {
            return JsonSerializer.SerializeToElement(value, JsonSerializerOptions);
        }

        public T DeserializeByteArray<T>(byte[] bytes)
        {
            return JsonSerializer.Deserialize<T>(bytes, JsonSerializerOptions)!;
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

            return element.Deserialize(targetType, JsonSerializerOptions);
        }

        // 3. Convierte un JsonElement a un objeto fuertemente tipado
        public T? DeserializeJsonElement<T>(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Null || element.ValueKind == JsonValueKind.Undefined)
                return default;

            return element.Deserialize<T>(JsonSerializerOptions);
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
                    list.Add(DeserializeJsonElement(elementEnum.Current.Clone(), typeEnum.Current));
                }

                return list;
            }
            catch (Exception ex)
            {
                logger.LogInformation(ex.ToString());
                return Enumerable.Empty<object?>();
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
                    yield return DeserializeJsonElement<T>(item.Clone())!;
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
                    yield return obj.Clone();
                }
            }
        }

        public string Serialize<T>(T value)
        {
            try
            {
                return JsonSerializer.Serialize(value, JsonSerializerOptions);
            }
            catch (Exception ex)
            {
                return "";
            }
        }

        public JsonElement SerializeToElement<T>(T value)
        {
            if (value == null)
                return default;

            try
            {
                return JsonSerializer.SerializeToElement(value, JsonSerializerOptions).Clone();
            }
            catch (Exception ex)
            {
                return default;
            }
        }

        public ReadOnlySpan<byte> SerializeToSpan<T>(T value, ArrayBufferWriter<byte> bufferWriter)
        {
            bufferWriter.Clear();
            using var writer = new Utf8JsonWriter(bufferWriter);
            JsonSerializer.Serialize(writer, value, JsonSerializerOptions);
            writer.Flush();
            return bufferWriter.WrittenSpan;
        }
    }

    public static class HubconJsonDefaults
    {
        private static readonly Lazy<JsonSerializerOptions> _options = new Lazy<JsonSerializerOptions>(() =>
        {
            // 1. Intentamos buscar la clase generada por el SG
            // Usamos el nombre de espacio y clase que definimos en el generador
            // Nota: Type.GetType requiere el nombre del ensamblado si no está en la misma DLL.
            // Para simplificar, buscamos en el Assembly que llamó a la librería.
            var generatedOptions = TryGetGeneratedOptions();

            if (generatedOptions != null)
            {
                return generatedOptions;
            }

            // 2. Fallback: Si no hay código generado, usamos la configuración manual
            // Advertencia: Esto usará reflexión en tiempo de ejecución (no ideal para AOT puro)
            return CreateFallbackOptions();
        });

        public static JsonSerializerOptions Options => _options.Value;

        private static JsonSerializerOptions? TryGetGeneratedOptions()
        {
            try
            {
                // Buscamos en todos los assemblies cargados (o podrías restringirlo)
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var type = assembly.GetType("Hubcon.Generated.HubconSerialization");
                    if (type != null)
                    {
                        var field = type.GetField("DefaultOptions", BindingFlags.Public | BindingFlags.Static);
                        if (field?.GetValue(null) is JsonSerializerOptions options)
                        {
                            return options;
                        }
                    }
                }
            }
            catch
            {
                // Si algo falla en la reflexión, ignoramos y vamos al fallback
            }
            return null;
        }

        private static JsonSerializerOptions CreateFallbackOptions()
        {
            return new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
                MaxDepth = 64,
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            };
        }
    }
}