using Hubcon.Shared.Abstractions.Interfaces;

namespace Hubcon.Shared.Abstractions.Models
{
    public record class BaseOperationResponse : BaseResponse, IObjectOperationResponse, IResponse
    {
        public object? Data { get; }
        public override bool Success { get; }
        public override string? Error { get; }

        public BaseOperationResponse(bool success, object? data = null, string? error = null)
        {
            Success = success;
            Data = data;
            Error = error;
        }
    }

    public record class BaseOperationResponse<T> : BaseResponse, IOperationResult, IOperationResponse<T>, IResponse
    {
        public override bool Success { get; }
        public override string? Error { get; }
        public T? Data { get; }
        object? IOperationResult.Data => Data;

        public BaseOperationResponse(bool success, T? data = default, string? error = null)
        {
            Success = success;
            Data = data;
            Error = error;
        }
    }
}
