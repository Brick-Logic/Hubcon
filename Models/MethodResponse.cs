using Hubcon.Converters;
using MessagePack;
using System.ComponentModel;
using System.Reflection;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Hubcon.Models
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MessagePackObject]
    public class MethodResponse
    {
        [Key(0)]
        public bool Success { get; set; } = false;

        [Key(1)]
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
