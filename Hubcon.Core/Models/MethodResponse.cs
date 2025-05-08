using Hubcon.Core.Converters;
using Hubcon.Core.Models.Interfaces;
using System.ComponentModel;
using System.Data.SqlTypes;
using System.Text.Json;

namespace Hubcon.Core.Models
{
    public interface IResponse
    {
        public bool Success { get; set; }
    }

    public abstract class BaseResponse : IResponse
    {
        public bool Success { get; set; }
    }

    public interface IMethodResponse : IMethodResponse<object>, IResponse
    {       
    }

    public interface IMethodResponse<T> : IResponse
    { 
        public T? Data { get; }
        public string? Error { get; }
    }

    public class BaseMethodResponse : BaseResponse, IMethodResponse, IResponse
    {
        public object? Data { get; }

        public string? Error { get; }

        public BaseMethodResponse()
        {
            
        }

        public BaseMethodResponse(bool success, object? data = null, string? error = null)
        {
            Success = success;
            Data = data;
            Error = error;
        }
    }

    public class BaseJsonResponse : BaseMethodResponse<JsonElement?>
    {
        public BaseJsonResponse() : base(true, null, null)
        {
            
        }

        public BaseJsonResponse(bool success, JsonElement? data = null, string? error = null) : base(success, data, error)
        {

        }
    }

    public class BaseMethodResponse<T> : BaseResponse, IMethodResponse<T>, IResponse
    {
        public T? Data { get; }
        public string? Error { get; }

        public BaseMethodResponse(bool success, T? data = default, string? error = null)
        {
            Success = success;
            Data = data;
            Error = error;
        }
    }
}
