using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

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
