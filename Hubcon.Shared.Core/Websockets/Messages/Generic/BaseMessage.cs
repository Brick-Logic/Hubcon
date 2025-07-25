using Hubcon.Shared.Core.Tools;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hubcon.Shared.Core.Websockets.Messages.Generic
{
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

        public BaseMessage(ReadOnlyMemory<byte> buffer)
        {
            _buffer = buffer;
        }

        protected T? Extract<T>(string propertyName)
        {
            if (_buffer is null)
                return default;

            var reader = new Utf8JsonReader(((ReadOnlyMemory<byte>)_buffer).Span, isFinalBlock: true, state: default);

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.PropertyName && reader.ValueTextEquals(propertyName))
                {
                    reader.Read();
                    return typeof(T) switch
                    {
                        Type t when t == typeof(Guid) => (T?)(object)reader.GetGuid()!,
                        Type t when t == typeof(string) => (T?)(object)reader.GetString()!,
                        Type t when t == typeof(MessageType) => Enum.TryParse(reader.GetString(), ignoreCase: true, out MessageType result) ? (T)(object)result : default,
                        Type t when t == typeof(JsonElement) => (T)(object)JsonDocument.ParseValue(ref reader).RootElement,
                        _ => default
                    };
                }
            }

            return default;
        }
    }
}