using Hubcon.Shared.Abstractions.Interfaces;
using Hubcon.Shared.Core.Websockets.Messages.Cancellation;
using Hubcon.Shared.Core.Websockets.Messages.Connection;
using Hubcon.Shared.Core.Websockets.Messages.Ingest;
using Hubcon.Shared.Core.Websockets.Messages.Operation;
using Hubcon.Shared.Core.Websockets.Messages.Ping;
using Hubcon.Shared.Core.Websockets.Messages.Streams;
using Hubcon.Shared.Core.Websockets.Messages.Subscriptions;
using Hubcon.Shared.Core.Websockets.Messages.Token;
using Hubcon.Shared.Core.Websockets.Models;
using System;
using System.Linq.Expressions;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;

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

    public class BaseMessage : IDisposable
    {
        private readonly TrimmedMemoryOwner? _buffer;
        private Guid? _id;
        private MessageType? _type;

        [JsonPropertyName("id")]
        public Guid Id => _id ??= Extract<Guid>("id");

        [JsonPropertyName("type")]
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

        public BaseMessage(TrimmedMemoryOwner buffer, Guid? id = null, MessageType? type = null)
        {
            if (id != null) _id = id;
            if (type != null) _type = type;

            _buffer = buffer;
        }

        //protected T? Extract<T>(string propertyName, bool isBinaryPayload = false)
        //{
        //    if (_buffer is null)
        //        return default;

        //    var span = _buffer.Value.Span;
        //    var reader = new Utf8JsonReader(span, isFinalBlock: true, state: default);

        //    if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        //        return default;

        //    if (isBinaryPayload && typeof(T) == typeof(byte[]))
        //    {
        //        int depth = 0;
        //        var binaryReader = new Utf8JsonReader(span, isFinalBlock: true, state: default);
        //        while (binaryReader.Read())
        //        {
        //            if (binaryReader.TokenType == JsonTokenType.StartObject) depth++;
        //            else if (binaryReader.TokenType == JsonTokenType.EndObject) depth--;

        //            if (depth == 0)
        //            {
        //                int payloadOffset = (int)binaryReader.BytesConsumed;
        //                return (T)(object)span.Slice(payloadOffset).ToArray();
        //            }
        //        }
        //        return default;
        //    }

        //    while (reader.Read())
        //    {
        //        if (reader.TokenType == JsonTokenType.StartObject || reader.TokenType == JsonTokenType.StartArray)
        //        {
        //            reader.Skip();
        //            continue;
        //        }

        //        if (reader.CurrentDepth == 1 &&
        //            reader.TokenType == JsonTokenType.PropertyName &&
        //            reader.ValueTextEquals(propertyName))
        //        {
        //            reader.Read();
        //            return typeof(T) switch
        //            {
        //                Type t when t == typeof(Guid) => Cast<T, Guid>(reader.GetGuid()),
        //                Type t when t == typeof(Guid[]) => Cast<T, Guid[]>(ReadGuidArray(ref reader)),
        //                Type t when t == typeof(bool) => Cast<T, bool>(reader.GetBoolean()),
        //                Type t when t == typeof(string) => Cast<T, string?>(reader.GetString()),
        //                Type t when t == typeof(MessageType) => Enum.TryParse(
        //                    reader.GetString(), 
        //                    ignoreCase: true, 
        //                    out MessageType result) 
        //                && Enum.IsDefined(typeof(MessageType), result)? Cast<T, MessageType>(result) : default,
        //                Type t when t == typeof(JsonElement) => Cast<T, JsonElement>(JsonDocument.ParseValue(ref reader).RootElement),
        //                Type t when t == typeof(object) => Cast<T, object>(JsonDocument.ParseValue(ref reader).RootElement),
        //                _ => default
        //            };
        //        }

        //        if (reader.TokenType == JsonTokenType.EndObject && reader.CurrentDepth == 0)
        //            break;
        //    }

        //    return default!;
        //}

        protected T? Extract<T>(string propertyName, bool isBinaryPayload = false)
        {
            if (_buffer is null) return default;

            var span = _buffer.Memory.Span;
            var reader = new Utf8JsonReader(span, isFinalBlock: true, state: default);

            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
                return default;

            // Manejo de Binario optimizado
            if (isBinaryPayload && typeof(T) == typeof(byte[]))
            {
                // En lugar de un segundo reader, podemos usar el progreso del primero
                // pero necesitamos llegar al final del objeto JSON actual.
                while (reader.Read()) { /* Consumir todo el JSON */ }
                int payloadOffset = (int)reader.BytesConsumed;
                return (T)(object)span.Slice(payloadOffset).ToArray();
            }

            while (reader.Read())
            {
                // Si encontramos un objeto o array que no es el que buscamos, lo saltamos
                if (reader.TokenType == JsonTokenType.StartObject || reader.TokenType == JsonTokenType.StartArray)
                {
                    reader.Skip();
                    continue;
                }

                if (reader.CurrentDepth == 1 &&
                    reader.TokenType == JsonTokenType.PropertyName &&
                    reader.ValueTextEquals(propertyName))
                {
                    reader.Read();

                    return typeof(T) switch
                    {
                        Type t when t == typeof(Guid) => (T)(object)reader.GetGuid(),
                        Type t when t == typeof(Guid[]) => (T)(object)ReadGuidArray(ref reader),
                        Type t when t == typeof(bool) => (T)(object)reader.GetBoolean(),
                        Type t when t == typeof(string) => (T?)(object?)reader.GetString(),
                        Type t when t == typeof(MessageType) => (T)(object)ParseMessageType(reader.GetString()),
                        Type t when t == typeof(JsonElement) => HandleJsonElement(ref reader),
                        _ => default
                    };

                    static T HandleJsonElement(ref Utf8JsonReader reader)
                    {
                        using var doc = JsonDocument.ParseValue(ref reader);
                        return (T)(object)doc.RootElement.Clone();
                    }
                }
            }
            return default;
        }

        // Método auxiliar AOT-Safe para evitar reflexión en Enums
        private MessageType ParseMessageType(string? value) => value switch
        {
            "connection_init" => MessageType.connection_init,
            "connection_ack" => MessageType.connection_ack,
            "error" => MessageType.error,
            "ack" => MessageType.ack,
            "operation_invoke" => MessageType.operation_invoke,
            "operation_response" => MessageType.operation_response,
            "ping" => MessageType.ping,
            "pong" => MessageType.pong,
            "subscription_init" => MessageType.subscription_init,
            "subscription_data" => MessageType.subscription_data,
            "subscription_data_with_ack" => MessageType.subscription_data_with_ack,
            "subscription_complete" => MessageType.subscription_complete,
            "ingest_init" => MessageType.ingest_init,
            "ingest_init_ack" => MessageType.ingest_init_ack,
            "ingest_data" => MessageType.ingest_data,
            "ingest_data_ack" => MessageType.ingest_data_ack,
            "ingest_complete" => MessageType.ingest_complete,
            "ingest_data_with_ack" => MessageType.ingest_data_with_ack,
            "operation_call" => MessageType.operation_call,
            "stream_init" => MessageType.stream_init,
            "stream_complete" => MessageType.stream_complete,
            "stream_data_ack" => MessageType.stream_data_ack,
            "stream_data" => MessageType.stream_data,
            "stream_data_with_ack" => MessageType.stream_data_with_ack,
            "ingest_result" => MessageType.ingest_result,
            "cancel" => MessageType.cancel,
            "token_update" => MessageType.token_update,
            "none" => MessageType.none,
            _ => MessageType.none
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static T Cast<T, TReal>(TReal value) => Unsafe.As<TReal, T>(ref value);

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

        public void Dispose()
        {
            _buffer?.Dispose();
        }
    }
}