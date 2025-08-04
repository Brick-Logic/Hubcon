using Hubcon.Shared.Core.Tools;
using System;
using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hubcon.Shared.Core.Websockets.Messages.Generic
{
    public static class MessageFactory<T> where T : BaseMessage
    {
        private static readonly Func<ReadOnlyMemory<byte>, Guid?, MessageType?, T> _ctor;

        static MessageFactory()
        {
            var bufferParam = Expression.Parameter(typeof(ReadOnlyMemory<byte>), "buffer");
            var idParam = Expression.Parameter(typeof(Guid?), "id");
            var typeParam = Expression.Parameter(typeof(MessageType?), "type");

            var ctorInfo = typeof(T).GetConstructor(
                new[] { typeof(ReadOnlyMemory<byte>), typeof(Guid?), typeof(MessageType?) }
            );

            if (ctorInfo == null)
                throw new InvalidOperationException($"Constructor no encontrado en {typeof(T).Name}");

            var newExpr = Expression.New(ctorInfo, bufferParam, idParam, typeParam);
            _ctor = Expression
                .Lambda<Func<ReadOnlyMemory<byte>, Guid?, MessageType?, T>>(newExpr, bufferParam, idParam, typeParam)
                .Compile();
        }

        public static T Create(ReadOnlyMemory<byte> buffer, Guid? id = null, MessageType? type = null) => _ctor(buffer, id, type);
    }

    public record class BaseMessage
    {
        private readonly ReadOnlyMemory<byte>? _buffer;
        private Guid? _id;
        private MessageType? _type;

        public Guid Id => _id ??= Extract<Guid>("id");
        public MessageType Type => _type ??= Extract<MessageType>("type");

        public BaseMessage()
        {

        }

        [JsonConstructor]
        public BaseMessage(MessageType type, Guid id)
        {
            _type = type;
            _id = id;
        }

        public BaseMessage(ReadOnlyMemory<byte> buffer, Guid? id = null, MessageType? type = null)
        {
            if (id != null) _id = id;
            if (type != null) _type = type;

            _buffer = buffer;
        }

        protected T? Extract<T>(string propertyName, bool isBinaryPayload = false)
        {
            if (_buffer is null)
                return default;

            var span = ((ReadOnlyMemory<byte>)_buffer).Span;
            var reader = new Utf8JsonReader(span, isFinalBlock: true, state: default);

            if (isBinaryPayload && typeof(T) == typeof(byte[]))
            {
                int depth = 0;
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.StartObject)
                        depth++;
                    else if (reader.TokenType == JsonTokenType.EndObject)
                        depth--;

                    if (depth == 0)
                    {
                        int payloadOffset = (int)reader.BytesConsumed;
                        var payloadSpan = span.Slice(payloadOffset);
                        return (T)(object)payloadSpan.ToArray();
                    }
                }

                return default;
            }

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.PropertyName && reader.ValueTextEquals(propertyName))
                {
                    reader.Read();
                    return typeof(T) switch
                    {
                        Type t when t == typeof(Guid) => (T)(object)reader.GetGuid(),
                        Type t when t == typeof(Guid[]) => (T)(object)ReadGuidArray(ref reader),
                        Type t when t == typeof(string) => (T)(object)reader.GetString()!,
                        Type t when t == typeof(MessageType) => Enum.TryParse(reader.GetString(), ignoreCase: true, out MessageType result) ? (T)(object)result : default,
                        Type t when t == typeof(JsonElement) => (T)(object)JsonDocument.ParseValue(ref reader).RootElement,
                        _ => default
                    };
                }
            }

            return default;
        }

        public T CreateMessage<T>() where T : BaseMessage
        {
            if (_buffer == null)
                return default!;

            return MessageFactory<T>.Create((ReadOnlyMemory<byte>)_buffer!, _id, _type);    
        }

        protected static Guid[] ReadGuidArray(ref Utf8JsonReader reader)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
                throw new JsonException("Expected StartArray token");

            var guids = new List<Guid>();

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                    break;

                if (reader.TokenType == JsonTokenType.String)
                {
                    if (reader.TryGetGuid(out var guid))
                        guids.Add(guid);
                    else
                        throw new JsonException("Invalid GUID format");
                }
                else
                {
                    throw new JsonException("Expected GUID string");
                }
            }

            return guids.ToArray();
        }
    }
}