using Hubcon.Shared.Abstractions.Interfaces;

namespace Hubcon.Shared.Abstractions.Models
{
    public class BaseJsonResponse<JsonElement> : BaseOperationResponse<JsonElement>, IOperationResponse<JsonElement>
    {
        public BaseJsonResponse(bool Success, JsonElement Data, string? Error) : base(Success, Data, Error)
        {
        }
    }
}
