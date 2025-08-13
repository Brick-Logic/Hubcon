using System.Text.Json;

namespace Hubcon.Shared.Core.Tools
{
    public static class JsonHelper
    {
        public static bool TryGetEnumFromJson<TEnum>(string jsonString, string propertyName, out TEnum result)
            where TEnum : struct, Enum
        {
            result = default;

            try
            {
                using var doc = JsonDocument.Parse(jsonString);
                if (doc.RootElement.TryGetProperty(propertyName, out var prop))
                {
                    string typeValue = prop.GetString() ?? string.Empty;
                    return Enum.TryParse<TEnum>(typeValue, ignoreCase: true, out result);
                }
            }
            catch (JsonException)
            {
                // JSON inválido
            }

            return false;
        }

        public static bool TryGetEnumFromJson<TEnum>(
            string jsonString, string propertyName,
            out TEnum result, out JsonElement resultElement)
            where TEnum : struct, Enum
        {
            result = default;
            resultElement = default;

            try
            {
                using var doc = JsonDocument.Parse(jsonString);

                if (!doc.RootElement.TryGetProperty(propertyName, out var prop))
                    return false;

                var typeValue = prop.GetString();
                if (string.IsNullOrEmpty(typeValue))
                    return false;

                if (Enum.TryParse<TEnum>(typeValue, ignoreCase: true, out result))
                {
                    resultElement = doc.RootElement.Clone();
                    return true;
                }
            }
            catch (JsonException)
            {
                // Ignorado: json inválido
            }

            return false;
        }
    }

}
