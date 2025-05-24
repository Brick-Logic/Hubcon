using Hubcon.Shared.Abstractions.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hubcon.Shared.Abstractions.Interfaces
{
    public interface IOperationRequest
    {
        IEnumerable<JsonElement> Args { get; }
        string ContractName { get; }
        string OperationName { get; }
    }
}
