using Hubcon.Core.Converters;
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
        public IMethodResponse<T> ToGeneric<T>() where T : class, IMethodResponse<T>, new()
        {
            return new T()
            {
                Success = this.Success,
                Data = (T?)this.Data
            };
        }

        public IMethodResponse<JsonElement?> ToJsonElement(DynamicConverter converter)
        {
            return new BaseMethodResponse<JsonElement?>(this.Success, converter.SerializeObject(this.Data)!);
        }

        public IMethodResponse<object?> FromGeneric()
        {
            return new BaseMethodResponse(this.Success, this.Data)!;
        }
    }

    public interface IMethodResponse<T> : IResponse
    { 
        public T? Data { get; set; }
    }

    public class BaseMethodResponse : BaseResponse, IMethodResponse, IResponse
    {
        public object? Data { get; set; }

        public BaseMethodResponse()
        {
            
        }

        public BaseMethodResponse(bool success, object? data = null)
        {
            Success = success;
            Data = data;
        }
    }

    public class BaseJsonResponse : BaseMethodResponse<JsonElement?>
    {
        public BaseJsonResponse() : base(true, null)
        {
            
        }

        public BaseJsonResponse(bool success, JsonElement? data = null) : base(success, data)
        {
        }
    }

    public class BaseMethodResponse<T> : BaseResponse, IMethodResponse<T>, IResponse
    {
        public T? Data { get; set; }


        public BaseMethodResponse(bool success, T? data = default)
        {
            Success = success;
            Data = data;
        }
    }
}
