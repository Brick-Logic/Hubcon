using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hubcon.Shared.Abstractions.Interfaces
{
    public interface IResponse
    {
        [Required]
        public bool Success { get; set; }

        [Required]
        public string Error { get; set; }
    }
}
