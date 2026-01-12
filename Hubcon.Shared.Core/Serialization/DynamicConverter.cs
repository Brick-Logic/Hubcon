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
using Hubcon.Shared.Core.Websockets.Messages.Subscriptions;
using Hubcon.Shared.Core.Websockets.Messages.Token;
using Hubcon.Shared.Core.Websockets.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Hubcon.Shared.Core.Serialization
{
    [JsonSerializable(typeof(JsonElement))]
    [JsonSerializable(typeof(IReadOnlyDictionary<string, object>))]
    [JsonSerializable(typeof(IOperationRequest))]
    [JsonSerializable(typeof(OperationRequest))]
    [JsonSerializable(typeof(SubscriptionRequest))]
    [JsonSerializable(typeof(Dictionary<string, object>))]
    [JsonSerializable(typeof(JsonObject))]
    [JsonSerializable(typeof(JsonArray))]
    [JsonSerializable(typeof(BaseOperationResponse<string>))]
    [JsonSerializable(typeof(BaseOperationResponse<bool>))]
    [JsonSerializable(typeof(BaseOperationResponse<int>))]
    [JsonSerializable(typeof(BaseOperationResponse<JsonElement>))]
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
    [JsonSerializable(typeof(SubscriptionCompleteMessage))]
    [JsonSerializable(typeof(SubscriptionDataMessage))]
    [JsonSerializable(typeof(SubscriptionInitMessage))]
    [JsonSerializable(typeof(TokenUpdateMessage))]
    [JsonSerializable(typeof(TokenUpdateResponseMessage))]
    [JsonSerializable(typeof(ConnectionInitMessage))]
    [JsonSerializable(typeof(ConnectionAckMessage))]
    [JsonSerializable(typeof(CancelMessage))]
    [JsonSerializable(typeof(Guid))]
    [JsonSerializable(typeof(MessageType))]
    public partial class SystemTypesContext : JsonSerializerContext
    {
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class DynamicConverter : IDynamicConverter
    {

        public ConcurrentDictionary<Delegate, Type[]> TypeCache { get; private set; } = new();

        public static JsonSerializerOptions JsonSerializerOptions { get; } = HubconJsonDefaults.Options;

        public static ConcurrentDictionary<Type, JsonTypeInfo> TypeInfoCache { get; private set; } = new();

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
            return HubconSerialization.GetOptions();
        });

        public static JsonSerializerOptions Options => _options.Value;      
    }

    public static class HubconSerialization
    {
        private static JsonSerializerOptions? _options;

        public static JsonSerializerOptions GetOptions()
        {
            if (_options == null) SetupJsonSerializerOption(new JsonSerializerOptions());
            return _options!;
        }

        public static void SetupJsonSerializerOption(JsonSerializerOptions? options) 
        { 
            if(_options == null)
            {
                _options = options;
                _options!.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                _options!.WriteIndented = false;
                _options!.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault;
                _options!.MaxDepth = 64;
                _options!.PropertyNameCaseInsensitive = true;
                _options!.Converters.Add(new JsonStringEnumConverter<Hubcon.Shared.Core.Websockets.MessageType>(JsonNamingPolicy.CamelCase));
            }
        }
    }
}


