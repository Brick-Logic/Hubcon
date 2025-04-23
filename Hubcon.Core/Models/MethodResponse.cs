using Hubcon.Core.Converters;
using System.ComponentModel;
using System.Text.Json;

namespace Hubcon.Core.Models
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface IMethodResponse
    { 
        public bool Success { get; set; }
        public object? Data { get; }
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public class BaseMethodResponse : IMethodResponse
    {
        public bool Success { get; set; } = false;

        public object? Data { get; private set; }


        public BaseMethodResponse(bool success, object? data = null)
        {
            Success = success;
            Data = data;
        }
    }
}
