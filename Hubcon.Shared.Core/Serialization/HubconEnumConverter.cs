using Hubcon.Shared.Core.Attributes;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hubcon
{
    public sealed class HubconEnumConverter<T> : JsonConverter<T> where T : struct, Enum
    {
        // El CLR garantiza que esto ocurra una sola vez y sea Lazy
        public readonly static HubconEnumConverter<T> Current = new();

        private readonly IImmutableDictionary<string, T> _mappingTable;
        private readonly IImmutableDictionary<T, string> _reverseMappingTable;
        private readonly T _fallbackValue;

        public HubconEnumConverter()
        {
            var _tempMappingTable = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
            var _tempReverseMappingTable = new Dictionary<T, string>();

            var enumType = typeof(T);
            var asNumber = enumType.IsDefined(typeof(JsonSerializeAsNumberAttribute));

            var fields = enumType.GetFields(BindingFlags.Public | BindingFlags.Static);
            bool fallbackIsDefault = false;
            // Resiliencia base: primer ítem
            if (fields.Length > 0) _fallbackValue = (T)fields[0].GetValue(null)!;

            foreach (var field in fields)
            {
                var value = (T)field.GetValue(null)!;

                // Lógica de mapeo centralizada
                _tempMappingTable[field.Name] = value;
                _tempMappingTable[Convert.ToInt64(value).ToString()] = value;

                var attr = field.GetCustomAttribute<JsonPropertyNameAttribute>();
                if (attr != null) _tempMappingTable[attr.Name] = value;

                if (fallbackIsDefault)
                    continue;

                if (field.IsDefined(typeof(JsonDefaultAttribute)))
                {
                    _fallbackValue = value;
                    fallbackIsDefault = true;
                }
                else if (field.Name.Equals("ParseError", StringComparison.OrdinalIgnoreCase))
                {
                    _fallbackValue = value;
                }
                else if (field.Name.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
                {
                    _fallbackValue = value;
                }
                else if (field.Name.Equals("Undefined", StringComparison.OrdinalIgnoreCase))
                {
                    _fallbackValue = value;
                }
            }

            foreach (var field in fields)
            {
                var value = (T)field.GetValue(null)!;
                var name = field.Name;
                var jsonAttr = field.GetCustomAttribute<JsonPropertyNameAttribute>();

                _tempMappingTable[name] = value;

                _tempMappingTable[Convert.ToInt64(value).ToString()] = value;

                if (jsonAttr != null) _tempMappingTable[jsonAttr.Name] = value;

                if (jsonAttr != null)
                {
                    _tempReverseMappingTable[value] = jsonAttr.Name; // Prioridad 1
                }
                else if (asNumber)
                {
                    _tempReverseMappingTable[value] = Convert.ToInt64(value).ToString();
                }
                else if (!_tempReverseMappingTable.ContainsKey(value))
                {
                    _tempReverseMappingTable[value] = name; // Prioridad 2 (Símbolo)
                }
            }

            _mappingTable = _tempMappingTable.ToImmutableDictionary();
            _reverseMappingTable = _tempReverseMappingTable.ToImmutableDictionary();
        }

        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            string? key = reader.TokenType switch
            {
                JsonTokenType.String => reader.GetString(),
                JsonTokenType.Number => Encoding.UTF8.GetString(reader.ValueSpan.ToArray()),
                _ => null
            };

            return (key != null && _mappingTable.TryGetValue(key, out var value))
                   ? value
                   : _fallbackValue;
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            if (_reverseMappingTable.TryGetValue(value, out var canonicalName))
            {
                writer.WriteStringValue(canonicalName);
            }
            else
            {
                writer.WriteNumberValue(Convert.ToInt64(value));
            }
        }
    }
}
