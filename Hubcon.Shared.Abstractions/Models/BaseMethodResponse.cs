using Hubcon.Shared.Abstractions.Interfaces;
using System.Text.Json.Serialization;

namespace Hubcon.Shared.Abstractions.Models
{
    public class BaseOperationResponse : BaseResponse, IResponse
    {
        public override bool Success { get; set; }

        public override string Error { get; set; } = "";

        public BaseOperationResponse(bool success, string error = "")
        {
            Success = success;
            Error = error;
        }
    }

    public class BaseOperationResponse<T> : BaseResponse, IOperationResult, IOperationResponse<T>, IResponse
    {
        public override bool Success { get; set; }

        public override string Error { get; set; }
        public int StatusCode { get; } = 200;
        public T Data { get; set; } = default(T)!;

        object IOperationResult.Data { get => this.Data!; set => Data = (T)value!; }

        public BaseOperationResponse(bool success, T data = default!, string error = default!)
        {
            Success = success;
            Data = data ?? default!;
            Error = error;
        }

        [JsonConstructor]
        public BaseOperationResponse(bool success, int statusCode, T data = default!, string error = default!)
        {
            Success = success;
            Data = data ?? default!;
            Error = error;
            StatusCode = statusCode;
        }
    }
}
