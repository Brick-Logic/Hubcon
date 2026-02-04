using Hubcon.Shared.Abstractions.Interfaces;

namespace Hubcon.Shared.Abstractions.Models
{
    public abstract class BaseResponse
    {
        public abstract bool Success { get; set; }

        public abstract string Error { get; set; }
    }
}
