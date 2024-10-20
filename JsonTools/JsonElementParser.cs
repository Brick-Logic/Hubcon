using System.Collections;
using System.Reflection;
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
                return type.IsValueType ? Activator.CreateInstance(type) : null; // Retorna null si el valor es JSON null
            }

            try
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    // Soporte para tipos Nullable<T>
                    type = Nullable.GetUnderlyingType(type);
                }

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
                        else if (type.IsEnum)
                        {
                            return Enum.Parse(type, stringValue);
                        }
                        return Convert.ChangeType(stringValue, type);

                    case JsonValueKind.Number:
                        return ConvertNumber(jsonElement, type);

                    case JsonValueKind.True:
                    case JsonValueKind.False:
                        return jsonElement.GetBoolean();

                    case JsonValueKind.Object:
                        return DeserializeObject(jsonElement, type);

                    case JsonValueKind.Array:
                        return DeserializeArray(jsonElement, type);

                    default:
                        throw new InvalidOperationException($"No se puede convertir el JsonElement de tipo {jsonElement.ValueKind}.");
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"No se puede convertir JsonElement a {type}: {ex.Message}", ex);
            }
        }

        private static object? ConvertNumber(JsonElement jsonElement, Type type)
        {
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
        }

        private static object? DeserializeArray(JsonElement jsonElement, Type type)
        {
            if (typeof(IEnumerable).IsAssignableFrom(type))
            {
                var elementType = type.IsGenericType ? type.GenericTypeArguments[0] : typeof(object); // Si no es genérico, usamos object
                var collection = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType)); // Usamos List<> como base

                foreach (var item in jsonElement.EnumerateArray())
                {
                    collection.Add(ConvertJsonElement(item, elementType)!);
                }

                // Intentar crear una instancia del tipo original, ya que podría no ser un genérico
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(HashSet<>))
                {
                    var hashSetType = typeof(HashSet<>).MakeGenericType(elementType);
                    return Activator.CreateInstance(hashSetType, collection);
                }

                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Queue<>))
                {
                    var queueType = typeof(Queue<>).MakeGenericType(elementType);
                    return Activator.CreateInstance(queueType, collection);
                }

                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Stack<>))
                {
                    var stackType = typeof(Stack<>).MakeGenericType(elementType);
                    return Activator.CreateInstance(stackType, collection);
                }

                // Para tipos que no son genéricos, pero que implementan IEnumerable
                if (!type.IsGenericType)
                {
                    var addMethod = type.GetMethod("Add");
                    if (addMethod != null)
                    {
                        var targetCollection = Activator.CreateInstance(type);
                        foreach (var item in collection)
                        {
                            addMethod.Invoke(targetCollection, new[] { item });
                        }
                        return targetCollection;
                    }
                }

                // Retorna la lista como el tipo correspondiente
                return Convert.ChangeType(collection, type);
            }

            return JsonSerializer.Deserialize(jsonElement.GetRawText(), type);
        }

        private static object? DeserializeObject(JsonElement jsonElement, Type type)
        {
            if (typeof(IDictionary).IsAssignableFrom(type))
            {
                var keyType = type.GenericTypeArguments[0];
                var valueType = type.GenericTypeArguments[1];
                var dictionary = (IDictionary)Activator.CreateInstance(type);

                foreach (var property in jsonElement.EnumerateObject())
                {
                    var key = Convert.ChangeType(property.Name, keyType);
                    var value = ConvertJsonElement(property.Value, valueType);
                    dictionary.Add(key, value);
                }

                return dictionary;
            }

            var instance = Activator.CreateInstance(type);

            // Obtener todas las propiedades en un diccionario insensible a mayúsculas
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                 .ToDictionary(p => p.Name.ToLowerInvariant(), p => p);

            foreach (var property in jsonElement.EnumerateObject())
            {
                // Verificar si existe la propiedad insensiblemente a mayúsculas
                if (properties.TryGetValue(property.Name.ToLowerInvariant(), out var prop))
                {
                    if (prop.CanWrite)
                    {
                        var convertedValue = ConvertJsonElement(property.Value, prop.PropertyType);
                        prop.SetValue(instance, convertedValue);
                    }
                }
                else if (prop.PropertyType.IsValueType && Nullable.GetUnderlyingType(prop.PropertyType) == null)
                {
                    // Si la propiedad es un tipo de valor no nulo y no existe en el JSON, lanzamos una excepción
                    throw new InvalidOperationException($"La propiedad {prop.Name} no se encontró en el JSON.");
                }
            }

            return instance;
        }

        private static object DeserializeTuple(JsonElement jsonElement, Type tupleType)
        {
            var genericArguments = tupleType.GetGenericArguments();
            JsonElement[] elements = jsonElement.ValueKind == JsonValueKind.Array
                ? jsonElement.EnumerateArray().ToArray()
                : jsonElement.EnumerateObject().Select(p => p.Value).ToArray();

            if (elements.Length != genericArguments.Length)
            {
                throw new InvalidOperationException("El número de elementos en el JSON no coincide con el número de elementos en la tupla.");
            }

            object[] tupleValues = new object[genericArguments.Length];
            for (int i = 0; i < genericArguments.Length; i++)
            {
                tupleValues[i] = ConvertJsonElement(elements[i], genericArguments[i])!;
            }

            return Activator.CreateInstance(tupleType, tupleValues);
        }
    }
}

