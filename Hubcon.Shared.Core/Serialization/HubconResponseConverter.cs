using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hubcon.Shared.Core.Serialization
{
    public class HubconResponseConverter : JsonConverter<HubconResponse>
    {
        public override HubconResponse? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            throw new NotSupportedException("Deserializing a HubconResponse<object> type is not supported.");
        }

        public override void Write(Utf8JsonWriter writer, HubconResponse value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
        
            writer.WriteBoolean("success", value.Success);
            writer.WriteBoolean("failed", value.Failure);
            if (value.Error != null) writer.WriteString("error", value.Error);
            if (value.Message != null) writer.WriteString("message", value.Message);
            
            writer.WriteNumber("statusCode", value.StatusCode);

            if (value.Data is not null)
            {
                writer.WritePropertyName("data");
                JsonSerializer.Serialize(writer, value.Data, value.Data.GetType(), options);
            }

            writer.WriteEndObject();
        }
    }
}