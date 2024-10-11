using System.Text.Json;

namespace Hubcon.JsonElementTools
{
    internal static class JsonElementParser
    {
        internal static T ConvertJsonElement<T>(object jsonElement)
        {
            return ConvertJsonElement<T>((JsonElement)jsonElement);
        }
        internal static T ConvertJsonElement<T>(JsonElement jsonElement)
        {
            // Si el JsonElement es nulo, retorna el valor por defecto
            if (jsonElement.ValueKind == JsonValueKind.Null)
            {
                return default; // O lanzar una excepción si prefieres
            }

            // Intentar convertir a string y luego a T
            try
            {
                // Obtener el valor como string
                string valueString = jsonElement.GetRawText();

                // Usar conversión genérica
                return (T)Convert.ChangeType(valueString, typeof(T));
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"No se puede convertir JsonElement a {typeof(T)}: {ex.Message}");
            }
        }
    }
}
