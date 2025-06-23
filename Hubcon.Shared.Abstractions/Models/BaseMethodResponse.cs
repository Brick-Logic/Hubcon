using Hubcon.Shared.Abstractions.Interfaces;
using System.ComponentModel.DataAnnotations;

namespace Hubcon.Shared.Abstractions.Models
{
    public record class BaseOperationResponse : BaseResponse, IResponse
    {
        [Required]
        public override bool Success { get; set; }

        [Required]
        public override string Error { get; set; } = "";

        public BaseOperationResponse(bool success, string error = "")
        {
            Success = success;
            Error = error;
        }
    }

    public record class BaseOperationResponse<T> : BaseResponse, IOperationResult, IOperationResponse<T>, IResponse
    {
        [Required]
        public override bool Success { get; set; }

        [Required]
        public override string Error { get; set; }

        [Required]
        public T Data { get; set; } = default(T)!;

        [Required]
        object IOperationResult.Data { get => this.Data!; set => Data = (T)value!; }

        public BaseOperationResponse(bool success, T data = default!, string error = default!)
        {
            Success = success;
            Data = data ?? default!;
            Error = error;
        }
    }
}
