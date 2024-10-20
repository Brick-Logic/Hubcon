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
                    case JsonValueKind.Array:
                        // Verificamos si el tipo destino es una tupla
                        if (IsTuple(type))
                        {
                            return DeserializeTuple(jsonElement, type);
                        }
                        // Manejo de arrays o deserialización estándar
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

        // Método que verifica si el tipo es una tupla
        private static bool IsTuple(Type type)
        {
            return type.IsGenericType && (
                type.GetGenericTypeDefinition() == typeof(ValueTuple<>) ||
                type.GetGenericTypeDefinition() == typeof(ValueTuple<,>) ||
                type.GetGenericTypeDefinition() == typeof(ValueTuple<,,>) ||
                type.GetGenericTypeDefinition() == typeof(ValueTuple<,,,>) ||
                type.GetGenericTypeDefinition() == typeof(ValueTuple<,,,,>) ||
                type.GetGenericTypeDefinition() == typeof(ValueTuple<,,,,,>) ||
                type.GetGenericTypeDefinition() == typeof(ValueTuple<,,,,,,>) ||
                type.GetGenericTypeDefinition() == typeof(ValueTuple<,,,,,,,>)
            );
        }

        // Método que deserializa tuplas
        private static object DeserializeTuple(JsonElement jsonElement, Type tupleType)
        {
            // Obtener los tipos de los elementos de la tupla
            var genericArguments = tupleType.GetGenericArguments();

            // Suponemos que el JSON es un array o un objeto con propiedades para cada campo de la tupla
            JsonElement[] elements = jsonElement.ValueKind == JsonValueKind.Array
                ? jsonElement.EnumerateArray().ToArray() // Si es un array
                : jsonElement.EnumerateObject().Select(p => p.Value).ToArray(); // Si es un objeto

            if (elements.Length != genericArguments.Length)
            {
                throw new InvalidOperationException("El número de elementos en el JSON no coincide con el número de elementos en la tupla.");
            }

            // Deserializar cada valor al tipo correspondiente en la tupla
            object[] tupleValues = new object[genericArguments.Length];
            for (int i = 0; i < genericArguments.Length; i++)
            {
                tupleValues[i] = ConvertJsonElement(elements[i], genericArguments[i])!;
            }

            // Crear una instancia de la tupla con los valores deserializados
            return Activator.CreateInstance(tupleType, tupleValues);
        }

    }
}
