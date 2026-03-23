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
    /// <summary>
    /// Hubcon's fast enum converter.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public sealed class HubconEnumConverter<T> : JsonConverter<T> where T : struct, Enum
    {
        /// <summary>
        /// Unique instance of this converter.
        /// </summary>
        public readonly static HubconEnumConverter<T> Current = new();

        private readonly IImmutableDictionary<string, T> _mappingTable;
        private readonly IImmutableDictionary<T, string> _reverseMappingTable;
        private readonly T _fallbackValue;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public HubconEnumConverter()
        {
            var _tempMappingTable = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
            var _tempReverseMappingTable = new Dictionary<T, string>();

            var enumType = typeof(T);
            var asNumber = enumType.IsDefined(typeof(JsonSerializeAsNumberAttribute));

            var fields = enumType.GetFields(BindingFlags.Public | BindingFlags.Static);
            bool fallbackIsDefault = false;

            if (fields.Length > 0) _fallbackValue = (T)fields[0].GetValue(null)!;

            foreach (var field in fields)
            {
                var value = (T)field.GetValue(null)!;

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
                    _tempReverseMappingTable[value] = jsonAttr.Name;
                }
                else if (asNumber)
                {
                    _tempReverseMappingTable[value] = Convert.ToInt64(value).ToString();
                }
                else if (!_tempReverseMappingTable.ContainsKey(value))
                {
                    _tempReverseMappingTable[value] = name;
                }
            }

            _mappingTable = _tempMappingTable.ToImmutableDictionary();
            _reverseMappingTable = _tempReverseMappingTable.ToImmutableDictionary();
        }

        /// <summary>
        /// Reads the enum
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="typeToConvert"></param>
        /// <param name="options"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Writes the enum.
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="value"></param>
        /// <param name="options"></param>
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
