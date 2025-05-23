using Hubcon.Shared.Abstractions.Interfaces;
using System.Text.Json;

namespace Hubcon.Shared.Core.Invocation
{
    public class BaseJsonResponse : BaseOperationResponse<JsonElement>, IOperationResponse<JsonElement>
    {
        public BaseJsonResponse() : base(true, default, null)
        {

        }

        public BaseJsonResponse(bool success, JsonElement data = default, string? error = null) : base(success, data, error)
        {

        }
    }
}
