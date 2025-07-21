using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Hubcon.Shared.Abstractions.Interfaces
{
    public interface IOperationResult : IResponse
    {
        [Required]
        [JsonRequired]
        public object Data { get; set; }
    }

    public interface IOperationResponse<T> : IOperationResult, IResponse
    {
        [Required]
        [JsonRequired]
        public new T Data { get; set; }
    }

    public interface IObjectOperationResponse : IOperationResponse<object>, IOperationResult, IResponse
    {
    }
}