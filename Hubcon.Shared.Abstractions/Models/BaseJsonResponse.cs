using Hubcon.Shared.Abstractions.Interfaces;
using System.Text.Json;

namespace Hubcon.Shared.Abstractions.Models
{
    public record class BaseJsonResponse(bool Success, JsonElement Data, string? Error) : BaseOperationResponse<JsonElement>(Success, Data, Error), IOperationResponse<JsonElement>
    {

    }
}
