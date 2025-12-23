using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Hubcon.Shared.Abstractions.Interfaces
{
    public interface IResponse
    {
        [Required]
        [JsonRequired]
        public bool Success { get; set; }

        [Required]
        [JsonRequired]
        public string Error { get; set; }
    }
}
