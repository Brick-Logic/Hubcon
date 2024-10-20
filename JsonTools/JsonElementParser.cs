using System.Text.Json;

namespace Hubcon.JsonElementTools
{
    internal static class JsonElementParser
    {
        internal static T? ConvertJsonElement<T>(object obj)
        {
            return (T?)ConvertJsonElement(obj, typeof(T));
        }

        internal static T? ConvertJsonElement<T>(JsonElement jsonElement)
        {
            return (T?)ConvertJsonElement(jsonElement, typeof(T));
        }

        internal static object? ConvertJsonElement(object jsonElement, Type type)
        {
            return ConvertJsonElement((JsonElement)jsonElement, type);
        }

        internal static object? ConvertJsonElement(JsonElement jsonElement, Type type)
        {
            if (jsonElement.ValueKind == JsonValueKind.Null)
            {
                return null; // Retorna null si el valor es JSON null
            }

            try
            {
                switch (jsonElement.ValueKind)
                {
                    case JsonValueKind.String:
                        string stringValue = jsonElement.GetString();
                        if (type == typeof(DateTime))
                        {
                            return DateTime.Parse(stringValue);
                        }
                        else if (type == typeof(Guid))
                        {
                            return Guid.Parse(stringValue);
                        }
                        return Convert.ChangeType(stringValue, type);

                    case JsonValueKind.Number:
                        if (type == typeof(int))
                        {
                            return jsonElement.GetInt32();
                        }
                        else if (type == typeof(long))
                        {
                            return jsonElement.GetInt64();
                        }
                        else if (type == typeof(decimal))
                        {
                            return jsonElement.GetDecimal();
                        }
                        else if (type == typeof(double))
                        {
                            return jsonElement.GetDouble();
                        }
                        else if (type == typeof(float))
                        {
                            return jsonElement.GetSingle();
                        }
                        return Convert.ChangeType(jsonElement.GetDouble(), type); // Conversión por defecto

                    case JsonValueKind.True:
                    case JsonValueKind.False:
                        return jsonElement.GetBoolean();

                    case JsonValueKind.Object:
                        // Deserializar a un tipo específico usando JsonSerializer
                        return JsonSerializer.Deserialize(jsonElement.GetRawText(), type);

                    case JsonValueKind.Array:
                        // Manejo de arrays: deserializa a un array o lista
                        return JsonSerializer.Deserialize(jsonElement.GetRawText(), type);

                    default:
                        throw new InvalidOperationException($"No se puede convertir el JsonElement de tipo {jsonElement.ValueKind}.");
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"No se puede convertir JsonElement a {type}: {ex.Message}", ex);
            }
        }
    }
}
