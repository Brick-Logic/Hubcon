using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Abstractions.Models;
using Hubcon.Shared.Core.Websockets;
using Hubcon.Shared.Core.Websockets.Messages.Cancellation;
using Hubcon.Shared.Core.Websockets.Messages.Connection;
using Hubcon.Shared.Core.Websockets.Messages.Generic;
using Hubcon.Shared.Core.Websockets.Messages.Ingest;
using Hubcon.Shared.Core.Websockets.Messages.Operation;
using Hubcon.Shared.Core.Websockets.Messages.Ping;
using Hubcon.Shared.Core.Websockets.Messages.Streams;
using Hubcon.Shared.Core.Websockets.Messages.Token;
using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace Hubcon.Shared.Core.Serialization
{
    /// <summary>
    /// Hubcon's internal json serializer context.
    /// </summary>
    [JsonSerializable(typeof(JsonElement))]
    [JsonSerializable(typeof(IReadOnlyDictionary<string, object>))]
    [JsonSerializable(typeof(IOperationRequest))]
    [JsonSerializable(typeof(OperationRequest))]
    [JsonSerializable(typeof(Dictionary<string, object>))]
    [JsonSerializable(typeof(JsonObject))]
    [JsonSerializable(typeof(JsonArray))]
    [JsonSerializable(typeof(HubconResponse<string>))]
    [JsonSerializable(typeof(HubconResponse<bool>))]
    [JsonSerializable(typeof(HubconResponse<int>))]
    [JsonSerializable(typeof(HubconResponse<JsonElement>))]
    [JsonSerializable(typeof(AckMessage))]
    [JsonSerializable(typeof(BaseMessage))]
    [JsonSerializable(typeof(ErrorMessage))]
    [JsonSerializable(typeof(IngestCompleteMessage))]
    [JsonSerializable(typeof(IngestDataAckMessage))]
    [JsonSerializable(typeof(IngestDataMessage))]
    [JsonSerializable(typeof(IngestInitAckMessage))]
    [JsonSerializable(typeof(IngestInitMessage))]
    [JsonSerializable(typeof(IngestResultMessage))]
    [JsonSerializable(typeof(OperationCallMessage))]
    [JsonSerializable(typeof(OperationInvokeMessage))]
    [JsonSerializable(typeof(OperationResponseMessage))]
    [JsonSerializable(typeof(PingMessage))]
    [JsonSerializable(typeof(PongMessage))]
    [JsonSerializable(typeof(StreamCompleteMessage))]
    [JsonSerializable(typeof(StreamInitMessage))]
    [JsonSerializable(typeof(TokenUpdateMessage))]
    [JsonSerializable(typeof(TokenUpdateResponseMessage))]
    [JsonSerializable(typeof(ConnectionInitMessage))]
    [JsonSerializable(typeof(ConnectionAckMessage))]
    [JsonSerializable(typeof(CancelMessage))]
    [JsonSerializable(typeof(Guid))]
    [JsonSerializable(typeof(MessageType))]
    [JsonSerializable(typeof(string))]
    // Enteros
    [JsonSerializable(typeof(int))]
    [JsonSerializable(typeof(int?))]
    [JsonSerializable(typeof(long))]
    [JsonSerializable(typeof(long?))]
    // Decimales
    [JsonSerializable(typeof(double))]
    [JsonSerializable(typeof(double?))]
    [JsonSerializable(typeof(float))]
    [JsonSerializable(typeof(float?))]
    [JsonSerializable(typeof(decimal))]
    [JsonSerializable(typeof(decimal?))]
    // Otros
    [JsonSerializable(typeof(bool))]
    [JsonSerializable(typeof(bool?))]
    [JsonSerializable(typeof(DateTime))]
    [JsonSerializable(typeof(DateTime?))]
    [JsonSerializable(typeof(DateTimeOffset))]
    [JsonSerializable(typeof(DateTimeOffset?))]
    [JsonSerializable(typeof(Guid))]
    [JsonSerializable(typeof(Guid?))]
    // Especiales
    [JsonSerializable(typeof(JsonElement))]
    [JsonSerializable(typeof(object))]
    public partial class SystemTypesContext : JsonSerializerContext
    {
    }

    /// <inheritdoc/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class DynamicConverter : IDynamicConverter
    {
        /// <inheritdoc/>
        public ConcurrentDictionary<Delegate, Type[]> TypeCache { get; private set; } = new();

        /// <summary>
        /// Hubcon's json serializer options.
        /// </summary>
        public static JsonSerializerOptions JsonSerializerOptions { get; } = HubconJsonDefaults.Options;

        /// <inheritdoc/>
        public static ConcurrentDictionary<Type, JsonTypeInfo> TypeInfoCache { get; private set; } = new();

        /// <inheritdoc/>
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

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="logger"></param>
        public DynamicConverter(ILogger<DynamicConverter> logger)
        {
            this.logger = logger;
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public T? DeserializeData<T>(object? data)
        {
            if (data == null)
                return default;

            var typeInfo = TypeInfoCache.GetOrAdd(typeof(T), x => JsonSerializerOptions.TypeInfoResolver!.GetTypeInfo(x, JsonSerializerOptions)!) as JsonTypeInfo<T>;

            if (data is JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.Null || element.ValueKind == JsonValueKind.Undefined)
                    return default;

                return element.Deserialize<T>(typeInfo!);
            }

            if (data is string text)
            {
                if (string.IsNullOrEmpty(text))
                    return default;

                return JsonSerializer.Deserialize(text, typeInfo!);
            }

            if (data is T t)
                return t;

            return default;
        }

        /// <inheritdoc/>
        public T? DeserializeFromString<T>(string? json)
        {
            if (string.IsNullOrEmpty(json))
                return default;

            return JsonSerializer.Deserialize<T>(json, JsonSerializerOptions);
        }

        /// <inheritdoc/>
        public JsonElement SerializeObject(object? value)
        {
            return JsonSerializer.SerializeToElement(value, JsonSerializerOptions);
        }

        /// <inheritdoc/>
        public T DeserializeByteArray<T>(byte[] bytes)
        {
            return JsonSerializer.Deserialize<T>(bytes, JsonSerializerOptions)!;
        }

        /// <inheritdoc/>
        public IEnumerable<JsonElement> SerializeArgsToJson(IEnumerable<object?> values)
        {
            List<JsonElement> results = new();

            foreach (var val in values)
            {
                results.Add(SerializeObject(val));
            }

            return results;
        }

        /// <inheritdoc/>
        public object? DeserializeJsonElement(JsonElement element, Type targetType)
        {
            if (element.ValueKind == JsonValueKind.Null)
                return null;

            return element.Deserialize(targetType, JsonSerializerOptions);
        }

        /// <inheritdoc/>
        public T? DeserializeJsonElement<T>(JsonElement element)
        {
            try
            {
                if (element.ValueKind == JsonValueKind.Null || element.ValueKind == JsonValueKind.Undefined)
                    return default;

                return element.Deserialize<T>(JsonSerializerOptions);
            }
            catch(Exception ex)
            {
                return default!;
            }
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
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

        /// <inheritdoc/>
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

        public Dictionary<string, object>? DeserializeParameters(IDictionary<string, object> dict, IDictionary<string, Type> parameterTypes)
        {
            if (dict.Count != parameterTypes.Count) return null;
            
            var resultDict = new Dictionary<string, object>();
            foreach (var key in parameterTypes.Keys)
            {
                if (dict.TryGetValue(key, out var item) && item is JsonElement element)
                {
                    resultDict[key] = DeserializeJsonElement(element, parameterTypes[key])!;
                }
            }

            return resultDict;
        }

        /// <inheritdoc/>
        public string Serialize<T>(T value)
        {
            try
            {
                Type type = value!.GetType();
                return JsonSerializer.Serialize(value, type, JsonSerializerOptions);
            }
            catch (Exception)
            {
                return "";
            }
        }

        /// <inheritdoc/>
        public JsonElement SerializeToElement<T>(T value)
        {
            if (value == null || (value is JsonElement element && (element.ValueKind == JsonValueKind.Undefined || element.ValueKind == JsonValueKind.Null)))
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

        /// <inheritdoc/>
        public ReadOnlySpan<byte> SerializeToSpan<T>(T value, ArrayBufferWriter<byte> bufferWriter)
        {
            bufferWriter.Clear();
            using var writer = new Utf8JsonWriter(bufferWriter);
            JsonSerializer.Serialize(writer, value, JsonSerializerOptions);
            writer.Flush();
            return bufferWriter.WrittenSpan;
        }

        /// <inheritdoc/>
        public void Serialize<T>(Utf8JsonWriter writer, T? message)
        {
            var typeInfo = TypeInfoCache.GetOrAdd(typeof(T), x => JsonSerializerOptions.TypeInfoResolver!.GetTypeInfo(x, JsonSerializerOptions)!) as JsonTypeInfo<T>;
            JsonSerializer.Serialize(writer, message, typeInfo!);
        }

        /// <inheritdoc/>
        public JsonElement ToJsonElement(string rawData)
        {
            // Intentamos ver si ya es un JSON válido (objeto o array)
            if ((rawData.StartsWith("{") && rawData.EndsWith("}")) ||
                (rawData.StartsWith("[") && rawData.EndsWith("]")))
            {
                try
                {
                    return JsonDocument.Parse(rawData).RootElement.Clone();
                }
                catch { }
            }

            return JsonSerializer.SerializeToElement(rawData, JsonSerializerOptions);
        }
    }

    /// <summary>
    /// Defaults for hubcon json serializer options.
    /// </summary>
    public static class HubconJsonDefaults
    {
        private static readonly Lazy<JsonSerializerOptions> _options = new Lazy<JsonSerializerOptions>(() =>
        {
            return HubconSerialization.GetOptions();
        });

        /// <summary>
        /// The default json serializer options.
        /// </summary>
        public static JsonSerializerOptions Options => _options.Value;      
    }

    /// <summary>
    /// Hubcon serialization setup class
    /// </summary>
    public static class HubconSerialization
    {
        private static JsonSerializerOptions? _options;

        /// <summary>
        /// Gets the json serializer options. Creates a new instance if the options are not set up.
        /// </summary>
        public static JsonSerializerOptions GetOptions()
        {
            if (_options == null) SetupJsonSerializerOption(new JsonSerializerOptions());
            return _options!;
        }

        /// <summary>
        /// Method used to configure the internal json serializer options using source generators.
        /// </summary>
        /// <param name="options"></param>
        public static void SetupJsonSerializerOption(JsonSerializerOptions? options) 
        {
            if (_options == null)
            {
                _options = options;
                _options!.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                _options!.WriteIndented = false;
                _options!.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault;
                _options!.MaxDepth = 64;
                _options!.PropertyNameCaseInsensitive = true;
                _options!.Converters.Add(new JsonStringEnumConverter<MessageType>(JsonNamingPolicy.CamelCase));

                if (SystemTypesContext.Default.Options.TypeInfoResolver != null)
                    _options.TypeInfoResolverChain.Add(SystemTypesContext.Default.Options.TypeInfoResolver);

                if (JsonSerializerOptions.Default.TypeInfoResolver != null)
                    _options.TypeInfoResolverChain.Add(JsonSerializerOptions.Default.TypeInfoResolver);

                foreach (var converter in JsonSerializerOptions.Default.Converters)
                    _options!.Converters.Add(converter);
            }
        }
    }
}


