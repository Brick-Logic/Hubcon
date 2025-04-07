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

        public MethodResponse SerializeData()
        {
            if (Data == null) return this;
            Data = DynamicConverter.SerializeData(Data);
            return this;
        }

        public T? GetDeserializedData<T>()
        {
            return DynamicConverter.DeserializeData<T>(Data!);
        }
    }
}
