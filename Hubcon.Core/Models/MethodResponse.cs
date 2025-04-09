using Hubcon.Core.Converters;
using System.ComponentModel;

namespace Hubcon.Core.Models
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class MethodResponse
    {
        public bool Success { get; set; } = false;

        public object? Data { get; set; }


        public MethodResponse(bool success, object? data = null)
        {
            Success = success;
            Data = data;
        }

        public MethodResponse SerializeData(Func<object?, object?> serializer)
        {
            if (Data == null) return this;
            Data = serializer.Invoke(Data);
            return this;
        }

        public T? GetDeserializedData<T>(Func<object?, T?> deserializer)
        {
            return deserializer.Invoke(Data!);
        }
    }
}
